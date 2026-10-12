// Composition root of the server (core-instructions/03-architecture.md): hosting, logging and service registration only.

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");

app.Run();
