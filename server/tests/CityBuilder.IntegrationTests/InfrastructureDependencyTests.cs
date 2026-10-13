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

        // Placeholder until Infrastructure has its first type (M0-19): then bind with typeof(thatType).Assembly instead of the name.
        // NotEmpty only shows that reflection returned data (every assembly references the runtime); it does not prove which assembly
        // was inspected. A project reference to Api is a cycle that MSBuild rejects, so today this guards file or reflection-based references.
        Assert.NotEmpty(names);
        Assert.DoesNotContain("CityBuilder.Api", names);
    }
}
