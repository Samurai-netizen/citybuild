using CityBuilder.Client.Net;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>
    /// Which server address a player uses (M0-82, <c>docs/BUILD.md</c>): the settings asset's, unless the player was started with
    /// <c>-server-url &lt;address&gt;</c>, so one build can point at another server without a code or asset change.
    /// </summary>
    public sealed class ServerAddressTests
    {
        /// <summary>The asset's address the settings hold.</summary>
        private const string AssetUrl = "http://localhost:8080";

        /// <summary>Without the option the asset's address is used.</summary>
        [Test]
        public void Resolve_WithoutTheOption_UsesTheAssetsAddress()
        {
            Assert.That(ServerAddress.Resolve(new[] { "CityBuilder", "-logFile", "-" }, AssetUrl), Is.EqualTo(AssetUrl));
        }

        /// <summary>The option's value replaces the asset's address.</summary>
        [Test]
        public void Resolve_WithTheOption_UsesItsValue()
        {
            string[] args = { "CityBuilder", "-server-url", "http://192.168.1.20:8080", "-logFile", "-" };

            Assert.That(ServerAddress.Resolve(args, AssetUrl), Is.EqualTo("http://192.168.1.20:8080"));
        }

        /// <summary>The <c>-server-url=&lt;address&gt;</c> form works too.</summary>
        [Test]
        public void Resolve_WithTheEqualsForm_UsesItsValue()
        {
            Assert.That(ServerAddress.Resolve(new[] { "CityBuilder", "-server-url=https://dev.example.org" }, AssetUrl), Is.EqualTo("https://dev.example.org"));
        }

        /// <summary>
        /// The option without a value gives an empty address, which <see cref="ApiClient"/> refuses: the player shows the settings error
        /// instead of quietly using the asset's address.
        /// </summary>
        [Test]
        public void Resolve_WithTheOptionLast_GivesAnEmptyAddressTheClientRefuses()
        {
            string address = ServerAddress.Resolve(new[] { "CityBuilder", "-server-url" }, AssetUrl);

            Assert.That(address, Is.Empty);
            Assert.That(() => new ApiClient(address, 15, new FakeTransport(), allowInsecureHttp: true), Throws.ArgumentException);
        }

        /// <summary>A null argument list (a platform without one) uses the asset's address.</summary>
        [Test]
        public void Resolve_WithNoArguments_UsesTheAssetsAddress()
        {
            Assert.That(ServerAddress.Resolve(null, AssetUrl), Is.EqualTo(AssetUrl));
        }
    }
}
