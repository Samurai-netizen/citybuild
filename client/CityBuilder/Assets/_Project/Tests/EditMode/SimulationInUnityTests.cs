using CityBuilder.Simulation;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode
{
    /// <summary>
    /// Proves that the local package <c>com.citybuilder.simulation</c> (<c>shared/Simulation</c>) compiles inside Unity and that its
    /// code runs here. The rules of the engine are tested in the server test projects; this only checks the wiring.
    /// </summary>
    public sealed class SimulationInUnityTests
    {
        [TestCase(180L, 4L, 45L)] // divides evenly: 180 worker-seconds of labor with 4 workers
        [TestCase(181L, 4L, 46L)] // a remainder rounds up, so a cycle never finishes early
        [TestCase(1L, 1000L, 1L)]
        [TestCase(0L, 7L, 0L)]
        public void CeilDiv_FromTheLocalPackage_RoundsUp(long dividend, long divisor, long expected)
        {
            Assert.That(IntegerMath.CeilDiv(dividend, divisor), Is.EqualTo(expected));
        }
    }
}
