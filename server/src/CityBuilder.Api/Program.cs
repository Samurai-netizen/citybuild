// Composition root of the server (core-instructions/03-architecture.md): hosting, logging and service registration only.

using CityBuilder.Api.Content;
using CityBuilder.Application.Content;
using CityBuilder.Infrastructure.Content;
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

builder.Services.AddProblemDetails();
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

var app = builder.Build();

// Load the content before the server accepts requests: an empty Content:FilePath or invalid content stops the start here, and the
// exception lists every error.
string contentFilePath = app.Services.GetRequiredService<IOptions<ContentOptions>>().Value.GetFullFilePath();
GameContent content = app.Services.GetRequiredService<IContentProvider>().Content;
ContentLog.ContentLoaded(app.Logger, content.ContentVersion, contentFilePath);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");

app.Run();
