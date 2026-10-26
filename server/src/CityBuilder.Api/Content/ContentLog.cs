namespace CityBuilder.Api.Content;

/// <summary>The log messages about content (source-generated, as the analyzers require: CA1848).</summary>
internal static partial class ContentLog
{
    /// <summary>
    /// Logs at startup which content was loaded and from where (event id 1000; ids 1000–1999 belong to content messages, decision 029).
    /// The source generator writes the method's body.
    /// </summary>
    /// <param name="logger">The logger of the class that loaded the content.</param>
    /// <param name="contentVersion">The loaded content's <c>contentVersion</c>, such as <c>mvp-1</c>.</param>
    /// <param name="contentFilePath">The full path of the file it was read from.</param>
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Content {ContentVersion} loaded from {ContentFilePath}")]
    public static partial void ContentLoaded(ILogger logger, string contentVersion, string contentFilePath);
}
