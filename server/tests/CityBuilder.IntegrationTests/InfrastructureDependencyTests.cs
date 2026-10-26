using System.Reflection;
using CityBuilder.Infrastructure.Content;

namespace CityBuilder.IntegrationTests;

/// <summary>
/// Guards the dependency rule for Infrastructure (core-instructions/03-architecture.md, dependency table). It lives here because
/// this project has no test project of its own and is the one that sees the Api composition root.
/// </summary>
public sealed class InfrastructureDependencyTests
{
    /// <summary>The Infrastructure assembly doesn't reference the Api assembly.</summary>
    [Fact]
    public void GetReferencedAssemblies_OfInfrastructure_DoNotIncludeApi()
    {
        Assembly infrastructure = typeof(ContentFileLoader).Assembly;

        string[] names = infrastructure.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty).ToArray();

        // Bound to Infrastructure's first type (M0-07b), so the inspected assembly is the one the build produced. NotEmpty shows that
        // reflection returned data (every assembly references the runtime). A project reference to Api is a cycle that MSBuild rejects, so
        // this guards file or reflection-based references.
        Assert.NotEmpty(names);
        Assert.DoesNotContain("CityBuilder.Api", names);
    }
}
