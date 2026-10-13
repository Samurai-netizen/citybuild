using System.Reflection;
using System.Runtime.Versioning;

namespace CityBuilder.Simulation.Tests;

/// <summary>Guards the dependency rule for Simulation (core-instructions/03-architecture.md, dependency table): the base class library only.</summary>
public sealed class SimulationDependencyTests
{
    private static readonly Assembly _simulation = typeof(ItemId).Assembly;

    [Fact]
    public void GetReferencedAssemblies_OfSimulation_AreExactlyNetStandard()
    {
        // The engine is built for netstandard2.1 (see Assembly_IsBuiltForNetStandard21), where every base class library type resolves to
        // the netstandard facade. Any other name, System.* included, is a package or a file reference that Unity's .NET Standard 2.1
        // profile does not provide, so there is no allow-list by name prefix.
        Assert.Equal(["netstandard"], ReferencedAssemblyNames(_simulation));
    }

    [Fact]
    public void ReferencedAssemblyNames_OfThisTestAssembly_IncludeSystemRuntimeAndTheSimulationAssembly()
    {
        // Keeps the check above from comparing against a list that is empty or filtered: this net10.0 assembly references System.Runtime
        // and the engine, and both must show up.
        string[] names = ReferencedAssemblyNames(typeof(SimulationDependencyTests).Assembly);

        Assert.Contains("System.Runtime", names);
        Assert.Contains("CityBuilder.Simulation", names);
    }

    [Fact]
    public void Assembly_IsBuiltForNetStandard21()
    {
        TargetFrameworkAttribute? target = _simulation.GetCustomAttribute<TargetFrameworkAttribute>();

        Assert.NotNull(target);
        Assert.Equal(".NETStandard,Version=v2.1", target.FrameworkName);
    }

    private static string[] ReferencedAssemblyNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
