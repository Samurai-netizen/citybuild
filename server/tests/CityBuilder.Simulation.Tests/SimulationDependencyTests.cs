using System.Reflection;
using System.Runtime.Versioning;

namespace CityBuilder.Simulation.Tests;

/// <summary>Guards the dependency rule for Simulation (core-instructions/03-architecture.md, dependency table): the base class library only.</summary>
public sealed class SimulationDependencyTests
{
    private static readonly Assembly _simulation = typeof(ItemId).Assembly;

    [Fact]
    public void GetReferencedAssemblies_OfSimulation_AreAllPartOfTheBaseClassLibrary()
    {
        Assert.Empty(NonBaseClassLibraryReferences(_simulation));
    }

    [Fact]
    public void GetReferencedAssemblies_OfSimulation_IncludeNetStandard()
    {
        // Keeps the check above from passing on an empty list: the engine is compiled against the netstandard facade.
        string[] names = _simulation.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty).ToArray();

        Assert.Contains("netstandard", names);
    }

    [Fact]
    public void NonBaseClassLibraryReferences_OfThisTestAssembly_IncludeTheSimulationAssembly()
    {
        // Keeps the check from using a filter that accepts everything: this assembly references Simulation, which is no part of the BCL.
        string[] outside = NonBaseClassLibraryReferences(typeof(SimulationDependencyTests).Assembly);

        Assert.Contains("CityBuilder.Simulation", outside);
    }

    [Fact]
    public void Assembly_IsBuiltForNetStandard21()
    {
        TargetFrameworkAttribute? target = _simulation.GetCustomAttribute<TargetFrameworkAttribute>();

        Assert.NotNull(target);
        Assert.Equal(".NETStandard,Version=v2.1", target.FrameworkName);
    }

    private static string[] NonBaseClassLibraryReferences(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => !IsBaseClassLibrary(name))
            .ToArray();

    // A netstandard2.1 assembly reaches the base class library through the netstandard facade; other builds use mscorlib or System.*.
    private static bool IsBaseClassLibrary(string assemblyName) =>
        assemblyName is "netstandard" or "mscorlib" or "System" || assemblyName.StartsWith("System.", StringComparison.Ordinal);
}
