using CityBuilder.Client.Net;
using NUnit.Framework;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>The config asset <c>Resources/Config/ApiClientSettings.asset</c> that <c>GameRoot</c> will load by name (M0-38): it exists, holds the dev server and a usable timeout, and builds a client.</summary>
    public sealed class ApiClientSettingsTests
    {
        /// <summary>The asset loads by the name the composition root uses and points at the development server of <c>docs/getting-started.md</c>.</summary>
        [Test]
        public void TheAsset_LoadsByName_AndPointsAtTheDevServer()
        {
            ApiClientSettings settings = Resources.Load<ApiClientSettings>("Config/ApiClientSettings");

            Assert.That(settings, Is.Not.Null, "Assets/_Project/Resources/Config/ApiClientSettings.asset is missing or has lost its script");
            Assert.That(settings.BaseUrl, Is.EqualTo("http://localhost:8080"));
            Assert.That(settings.RequestTimeoutSeconds, Is.EqualTo(15));
        }

        /// <summary>The asset names the dev player the client logs in as, in the form the server accepts (<c>05</c>: 1 to 32 letters, digits, <c>_</c>, <c>-</c>, <c>.</c>).</summary>
        [Test]
        public void TheAsset_NamesAValidDevPlayer()
        {
            ApiClientSettings settings = Resources.Load<ApiClientSettings>("Config/ApiClientSettings");

            Assert.That(settings.DevDisplayName, Is.EqualTo("dev1"));
            Assert.That(settings.DevDisplayName, Does.Match("^[A-Za-z0-9_.-]{1,32}$"));
        }

        /// <summary>The asset's values are accepted by <see cref="ApiClient"/>, which refuses a wrong URL or timeout at construction.</summary>
        [Test]
        public void TheAssetsValues_BuildAClient()
        {
            ApiClientSettings settings = Resources.Load<ApiClientSettings>("Config/ApiClientSettings");

            Assert.That(() => new ApiClient(settings.BaseUrl, settings.RequestTimeoutSeconds, new FakeTransport()), Throws.Nothing);
        }
    }
}
