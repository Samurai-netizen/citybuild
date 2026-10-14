using CityBuilder.Simulation.Content;

namespace CityBuilder.Application.Content;

/// <summary>
/// The game content the server runs on (core-instructions/03-architecture.md, content pipeline). It is loaded and validated once,
/// before the server accepts requests, and stays the same for every request until the server restarts.
/// </summary>
public interface IContentProvider
{
    /// <summary>The validated content; never null, because a server with invalid content does not start.</summary>
    GameContent Content { get; }
}
