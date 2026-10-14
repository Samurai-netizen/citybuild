using CityBuilder.Application.Content;
using CityBuilder.Simulation.Content;

namespace CityBuilder.Infrastructure.Content;

/// <summary>Serves content that was loaded once (<see cref="ContentFileLoader"/>); the Api registers it as a singleton.</summary>
/// <param name="content">The validated content.</param>
public sealed class ContentProvider(GameContent content) : IContentProvider
{
    /// <inheritdoc />
    public GameContent Content { get; } = content ?? throw new ArgumentNullException(nameof(content));
}
