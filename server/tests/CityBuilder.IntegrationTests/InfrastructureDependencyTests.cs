using System.Reflection;

namespace CityBuilder.IntegrationTests;

/// <summary>
/// Guards the dependency rule for Infrastructure (core-instructions/03-architecture.md, dependency table). It lives here because
/// this project has no test project of its own and is the one that sees the Api composition root.
/// </summary>
public sealed class InfrastructureDependencyTests
{
    [Fact]
    public void GetReferencedAssemblies_OfInfrastructure_DoNotIncludeApi()
    {
        Assembly infrastructure = Assembly.Load(new AssemblyName("CityBuilder.Infrastructure"));

        string[] names = infrastructure.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty).ToArray();

        // Every assembly references at least the runtime, so an empty list would mean the check looked at nothing.
        Assert.NotEmpty(names);
        Assert.DoesNotContain("CityBuilder.Api", names);
    }
}
