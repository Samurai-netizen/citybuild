using System.Reflection;
using CityBuilder.Application.Content;

namespace CityBuilder.Application.Tests;

/// <summary>Guards the dependency rule for Application (core-instructions/03-architecture.md, dependency table).</summary>
public sealed class ApplicationDependencyTests
{
    /// <summary>Frameworks that Application must never depend on: use cases reach HTTP and the database only through ports.</summary>
    private static readonly string[] _forbiddenAssemblyPrefixes = ["Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore"];

    /// <summary>The Application assembly references neither ASP.NET Core nor Entity Framework Core.</summary>
    [Fact]
    public void GetReferencedAssemblies_OfApplication_ContainNoAspNetCoreOrEntityFrameworkCore()
    {
        // Bound to a type, so the test inspects the assembly the build produced and fails to compile if the type moves away.
        Assembly application = typeof(IContentProvider).Assembly;

        string[] forbidden = application.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => _forbiddenAssemblyPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();

        Assert.Empty(forbidden);
    }
}
