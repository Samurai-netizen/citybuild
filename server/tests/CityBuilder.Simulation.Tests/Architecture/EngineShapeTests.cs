using System.Reflection;

namespace CityBuilder.Simulation.Tests.Architecture;

/// <summary>
/// Guards the shape of the engine's compiled types (<c>.claude/rules/simulation.md</c>): state can only change through the rules, so no
/// type has a public setter or a public mutable field, and no field holds a floating-point or decimal number. The one exception is the
/// <c>Content.Data</c> namespace, the JSON-shaped classes that the content loader fills and the factory turns into definitions.
/// </summary>
public sealed class EngineShapeTests
{
    /// <summary>The compiled engine assembly.</summary>
    private static readonly Assembly _simulation = typeof(ItemId).Assembly;

    /// <summary>The namespace whose classes are JSON data holders with public setters, by design.</summary>
    private const string _dataNamespace = "CityBuilder.Simulation.Content.Data";

    /// <summary>All flags that reach every declared member of a type, whatever its access level.</summary>
    private const BindingFlags _allDeclared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>No engine type outside the JSON data holders exposes a setter or a mutable field to other types.</summary>
    [Fact]
    public void Types_OfSimulation_HaveNoPublicSetterOrMutableField()
    {
        string[] problems = StateTypes()
            .SelectMany(type => type.GetProperties(_allDeclared)
                .Where(property => property.SetMethod is { IsPublic: true } or { IsFamily: true } or { IsFamilyOrAssembly: true })
                .Select(property => $"{type.FullName}.{property.Name} has a setter"))
            .Concat(StateTypes().SelectMany(type => type.GetFields(_allDeclared)
                .Where(field => (field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly) && !field.IsInitOnly && !field.IsLiteral)
                .Select(field => $"{type.FullName}.{field.Name} is a mutable field")))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(problems.Length == 0, string.Join("\n", problems));
    }

    /// <summary>The check above covers the engine's state types, not an empty list, and it does see the data holders' setters that it exempts.</summary>
    [Fact]
    public void StateTypes_IncludeCityStateAndExcludeTheDataHolders()
    {
        Type[] state = StateTypes().ToArray();

        Assert.Contains(typeof(CityBuilder.Simulation.State.CityState), state);
        Assert.DoesNotContain(state, type => type.Namespace == _dataNamespace);
        Assert.Contains(_simulation.GetTypes(), type => type.Namespace == _dataNamespace
            && type.GetProperties().Any(property => property.SetMethod is { IsPublic: true }));
    }

    /// <summary>No field of any engine type, at any access level and compiler-made backing fields included, holds a float, a double or a decimal.</summary>
    [Fact]
    public void Fields_OfSimulation_HoldNoFloatingPointOrDecimal()
    {
        string[] problems = _simulation.GetTypes()
            .SelectMany(type => type.GetFields(_allDeclared)
                .Where(field => HoldsFloatingPoint(field.FieldType))
                .Select(field => $"{type.FullName}.{field.Name} is a {field.FieldType.Name}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(problems.Length == 0, string.Join("\n", problems));
    }

    /// <summary>The floating-point check looks inside arrays, nullable values and generic arguments, so a <c>List&lt;double&gt;</c> is caught.</summary>
    [Theory]
    [InlineData(typeof(double), true)]
    [InlineData(typeof(float?), true)]
    [InlineData(typeof(decimal[]), true)]
    [InlineData(typeof(List<double>), true)]
    [InlineData(typeof(Dictionary<string, long>), false)]
    [InlineData(typeof(int), false)]
    public void HoldsFloatingPoint_OfType_LooksInsideContainers(Type type, bool expected) =>
        Assert.Equal(expected, HoldsFloatingPoint(type));

    /// <summary>The engine's types that hold or expose state: every type except enums (their <c>value__</c> field is the compiler's), compiler-generated ones and the JSON data holders.</summary>
    private static IEnumerable<Type> StateTypes() =>
        _simulation.GetTypes().Where(type => type.Namespace != _dataNamespace && !type.IsEnum
            && type.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() is null);

    /// <summary>True when the type is a float, double or decimal, or an array, nullable or generic type that contains one.</summary>
    private static bool HoldsFloatingPoint(Type type)
    {
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
        {
            return true;
        }

        if (type.HasElementType)
        {
            return HoldsFloatingPoint(type.GetElementType()!);
        }

        return type.IsGenericType && type.GetGenericArguments().Any(HoldsFloatingPoint);
    }
}
