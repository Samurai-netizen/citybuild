namespace CityBuilder.MetricsReport;

/// <summary>One line of an explorer row's detail: an event or checkpoint title, how many players, and their share.</summary>
/// <param name="Title">The event's title (<c>event_type / detail / outcome</c>) or the named checkpoint's title.</param>
/// <param name="Players">The players it counts.</param>
/// <param name="Share">Their share of the players the list is about.</param>
public sealed record DetailCount(string Title, int Players, Rate Share);
