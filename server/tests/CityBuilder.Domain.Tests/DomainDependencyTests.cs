using System.Reflection;

namespace CityBuilder.Domain.Tests;

/// <summary>Guards the dependency rule for Domain (core-instructions/03-architecture.md, dependency table).</summary>
public sealed class DomainDependencyTests
{
    // Frameworks that Domain must never depend on: it holds pure server-side concepts without HTTP or database code.
    private static readonly string[] _forbiddenAssemblyPrefixes = ["Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore"];

    [Fact]
    public void GetReferencedAssemblies_OfDomain_ContainNoAspNetCoreOrEntityFrameworkCore()
    {
        Assembly domain = Assembly.Load(new AssemblyName("CityBuilder.Domain"));

        string[] forbidden = domain.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => _forbiddenAssemblyPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(forbidden);
    }
}
