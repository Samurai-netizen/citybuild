using System.Reflection;
using CityBuilder.Application.Metrics;

namespace CityBuilder.Application.Tests.Metrics;

/// <summary>
/// The shape of <see cref="IPlayerEventRepository"/> (CR-0009): game code can only add events, never read them, so no rule, cost or
/// unlock can depend on them (<c>02</c> rule 23). A read member added later fails here.
/// </summary>
public sealed class PlayerEventRepositoryShapeTests
{
    /// <summary>The port has one member, <c>Add</c>, which returns nothing: no query, no count, no property.</summary>
    [Fact]
    public void IPlayerEventRepository_HasNoReadMember()
    {
        MemberInfo[] members = typeof(IPlayerEventRepository).GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        MethodInfo add = Assert.IsAssignableFrom<MethodInfo>(Assert.Single(members));
        Assert.Equal((nameof(IPlayerEventRepository.Add), typeof(void)), (add.Name, add.ReturnType));
        Assert.Empty(typeof(IPlayerEventRepository).GetInterfaces());
    }
}
