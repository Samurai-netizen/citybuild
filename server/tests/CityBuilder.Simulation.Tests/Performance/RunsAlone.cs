namespace CityBuilder.Simulation.Tests.Performance;

/// <summary>
/// The test collection of the tests that time the engine: it runs alone, after every test that runs in parallel. Beside the property suite
/// (busy cores, frequent garbage collections) one advance of the worst case took up to 427 ms instead of about 40 ms, which would make the
/// CI limit flaky.
/// </summary>
[CollectionDefinition(nameof(RunsAlone), DisableParallelization = true)]
public sealed class RunsAlone;
