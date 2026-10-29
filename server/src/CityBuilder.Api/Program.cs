// Composition root of the server (core-instructions/03-architecture.md): hosting, logging, service registration, request limits,
// authentication and the startup steps (content load; database settings and, in Development only, the migrations).

using CityBuilder.Api.Auth;
using CityBuilder.Api.Content;
using CityBuilder.Api.Endpoints;
using CityBuilder.Api.Http;
using CityBuilder.Api.Persistence;
using CityBuilder.Application.Cities;
using CityBuilder.Application.Commands;
using CityBuilder.Application.Content;
using CityBuilder.Application.Players;
using CityBuilder.Infrastructure.Content;
using CityBuilder.Infrastructure.Persistence;
using CityBuilder.Simulation.Content;
using Microsoft.Extensions.Options;

// ISO 8601 in UTC (see UseUtcTimestamp), so log lines sort and parse the same way on every machine. A literal "Z"
// instead of "+00:00", because the JSON writer would turn the "+" into a six-character Unicode escape.
const string LogTimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.UseUtcTimestamp = true;
    options.TimestampFormat = LogTimestampFormat;
});

// Every error is Problem Details with a code from the catalogue (core-instructions/05-api-contract.md): a body that doesn't bind throws
// BadHttpRequestException in every environment (by default only in Development, where the developer exception page would echo the
// request headers, the token included), and ApiExceptionHandler answers it and every other exception.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

// What a request may send (core-instructions/07-security-model.md): no body above RequestLimits:MaxBodyBytes is read, and a JSON body that
// names a property twice is refused instead of read as its last value, so every reader of the body sees the same values.
builder.Services.AddRequestBodyLimit(builder.Configuration);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.AllowDuplicateProperties = false);
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

// The server clock is the only clock that drives game progress (core-instructions/02-principles.md, rule 3).
builder.Services.AddSingleton(TimeProvider.System);

// The content is loaded and validated once (core-instructions/03-architecture.md, content pipeline).
builder.Services.AddOptions<ContentOptions>()
    .Bind(builder.Configuration.GetSection(ContentOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.FilePath), "Content:FilePath must name the content file.")
    .ValidateOnStart();
builder.Services.AddSingleton<IContentProvider>(services =>
    new ContentProvider(ContentFileLoader.Load(services.GetRequiredService<IOptions<ContentOptions>>().Value.GetFullFilePath())));

// The database (core-instructions/06-data-model.md). The connection string holds a password: user-secrets or the environment only.
builder.Services.AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .Configure(options => options.ConnectionString =
        builder.Configuration.GetConnectionString(DatabaseOptions.ConnectionStringName) ?? string.Empty)
    .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString),
        "ConnectionStrings:CityBuilder must hold the database connection string (user-secrets or the environment variable " +
        "ConnectionStrings__CityBuilder; docs/getting-started.md → Database).")
    .Validate(options => !options.ApplyMigrationsAtStartup || builder.Environment.IsDevelopment(),
        "Database:ApplyMigrationsAtStartup may be true only in Development; apply migrations elsewhere on purpose " +
        "(docs/how-to/add-migration.md).")
    .Validate(options => options.LockTimeoutMilliseconds is >= DatabaseOptions.MinLockTimeoutMilliseconds
            and <= DatabaseOptions.MaxLockTimeoutMilliseconds,
        $"Database:LockTimeoutMilliseconds must be between {DatabaseOptions.MinLockTimeoutMilliseconds} and " +
        $"{DatabaseOptions.MaxLockTimeoutMilliseconds}.")
    .ValidateOnStart();
builder.Services.AddCityBuilderPersistence(services =>
{
    DatabaseOptions options = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    return new PersistenceSettings(options.ConnectionString, options.LockTimeoutMilliseconds);
});

// The access tokens: every /api/v1 endpoint needs one, and the acting player comes only from it (core-instructions/07-security-model.md).
builder.Services.AddCityBuilderAuthentication(builder.Configuration, builder.Environment);

// The use cases, scoped like the city transaction they run in (one per request). GET /city runs SettleCity; the command
// endpoints run every command through CommandPipeline.
builder.Services.AddScoped<SettleCity>();
builder.Services.AddScoped<CommandPipeline>();
builder.Services.AddScoped<DevLogin>();

var app = builder.Build();

// Load the content before the server accepts requests: an empty Content:FilePath or invalid content stops the start here, and the
// exception lists every error.
string contentFilePath = app.Services.GetRequiredService<IOptions<ContentOptions>>().Value.GetFullFilePath();
GameContent content = app.Services.GetRequiredService<IContentProvider>().Content;
ContentLog.ContentLoaded(app.Logger, content.ContentVersion, contentFilePath);

// Check the database settings before the server accepts requests, as for the content. In Development the pending migrations are
// applied now; the validation above keeps that from happening anywhere else, and the environment check repeats the rule here.
DatabaseOptions database = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
if (database.ApplyMigrationsAtStartup && app.Environment.IsDevelopment())
{
    IReadOnlyList<string> appliedMigrations = await DatabaseMigrator.MigrateAsync(app.Services, app.Lifetime.ApplicationStopping);
    string appliedList = appliedMigrations.Count == 0 ? "none" : string.Join(", ", appliedMigrations);
    DatabaseLog.MigrationsApplied(app.Logger, appliedMigrations.Count, appliedList);
}

// Check the authentication settings before the server accepts requests, as for the database: a missing or short signing key, or the dev
// login on outside Development, stops the start here.
AuthOptions auth = app.Services.GetRequiredService<IOptions<AuthOptions>>().Value;

// The exception handler first, so it answers whatever the rest of the pipeline throws; the status code pages next, so an empty 404, 405 or
// 415 gets a Problem Details body too (without a code: the catalogue has none for them). Then the body limit, so a body above it is refused
// before the token is checked or anything reads the body.
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRequestBodyLimit();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");

// The game API. Every endpoint of the group requires an access token unless it opts out; the acting player comes only from the token.
RouteGroupBuilder api = app.MapGroup("/api/v1").RequireAuthorization();

// The dev login lets anyone in by name alone: mapped only in Development with Auth:DevLoginEnabled on (the start refuses the setting
// anywhere else), so in Production the path answers 404.
if (auth.DevLoginEnabled && app.Environment.IsDevelopment())
{
    api.MapDevLogin();
}

// The game endpoints: all of them require the token.
api.MapContent();
api.MapCity();
api.MapCommands();

app.Run();
