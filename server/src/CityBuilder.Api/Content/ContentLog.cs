namespace CityBuilder.Api.Content;

/// <summary>The log messages about content (source-generated, as the analyzers require: CA1848).</summary>
internal static partial class ContentLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Content {ContentVersion} loaded from {ContentFilePath}")]
    public static partial void ContentLoaded(ILogger logger, string contentVersion, string contentFilePath);
}
