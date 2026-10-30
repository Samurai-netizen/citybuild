#nullable enable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CityBuilder.Client.Net;
using CityBuilder.Simulation;
using CityBuilder.Simulation.Content;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>
    /// <see cref="ContentService"/> and <see cref="ContentMapper"/> (M0-38): a real <c>GET /content</c> answer (<c>Tests/Fixtures/content.json</c>)
    /// becomes the engine's <see cref="GameContent"/> through the validating factory; a failed request and a refused answer are results.
    /// </summary>
    public sealed class ContentServiceTests
    {
        /// <summary>The fake wire of the current test.</summary>
        private FakeTransport _transport = null!;

        /// <summary>The service under test, over a client that carries a token.</summary>
        private ContentService _service = null!;

        /// <summary>Before each test: a service over a fake wire; the client holds a token as after a login.</summary>
        [SetUp]
        public void CreateService()
        {
            _transport = new FakeTransport();
            var api = new ApiClient("http://localhost:8080", 15, _transport);
            api.SetAccessToken("tok-1");
            _service = new ContentService(api);
        }

        /// <summary>The real answer gives valid content, and the request is an authenticated <c>GET /api/v1/content</c>.</summary>
        [Test]
        public async Task Load_TheRealAnswer_GivesValidContent()
        {
            _transport.Answer(200, Fixtures.Read("content.json"));

            ContentLoadResult result = await _service.LoadAsync(CancellationToken.None);

            Assert.That(result.IsSuccess, Is.True, Describe(result));
            Assert.That(result.ApiError, Is.Null);
            Assert.That(result.IsInvalidContent, Is.False);
            Assert.That(_transport.Requests, Has.Count.EqualTo(1));
            Assert.That(_transport.Requests[0].Method, Is.EqualTo("GET"));
            Assert.That(_transport.Requests[0].Url, Is.EqualTo("http://localhost:8080/api/v1/content"));
            Assert.That(_transport.Requests[0].AccessToken, Is.EqualTo("tok-1"));
        }

        /// <summary>The content carries the numbers of the answer: version, rules, the starter city, the era and the items.</summary>
        [Test]
        public async Task Load_TheRealAnswer_CarriesRulesStartingCityEraAndItems()
        {
            _transport.Answer(200, Fixtures.Read("content.json"));

            GameContent content = (await _service.LoadAsync(CancellationToken.None)).Content!;

            Assert.That(content.ContentVersion, Is.EqualTo("mvp-1"));
            Assert.That(content.Rules.GridWidth, Is.EqualTo(8));
            Assert.That(content.Rules.BuilderSlots, Is.EqualTo(2));
            Assert.That(content.Rules.TryGetBaseStorageCap(new ItemId("planks"), out long planksCap), Is.True);
            Assert.That(planksCap, Is.EqualTo(500L));
            Assert.That(content.StartingCity.Era, Is.EqualTo(1));
            Assert.That(content.StartingCity.Inventory.Single(q => q.Item.Value == "coins").Quantity, Is.EqualTo(700L));
            Assert.That(content.TryGetEra(1, out EraDefinition? era), Is.True);
            Assert.That(era!.Name, Is.EqualTo("Early Middle Ages"));
            Assert.That(content.Items.Select(item => item.Id.Value), Is.EqualTo(new[] { "coins", "logs", "planks" }));
        }

        /// <summary>Buildings keep their kind, recipe and levels: a house has no recipe, a sawmill has one, and a gated type names its research.</summary>
        [Test]
        public async Task Load_TheRealAnswer_CarriesBuildingsAndResearch()
        {
            _transport.Answer(200, Fixtures.Read("content.json"));
            ContentResponseDto dto = new ApiJson().Deserialize<ContentResponseDto>(Fixtures.Read("content.json"))!;

            GameContent content = (await _service.LoadAsync(CancellationToken.None)).Content!;

            Assert.That(content.Buildings, Has.Count.EqualTo(dto.Buildings.Count));
            Assert.That(content.Research, Has.Count.EqualTo(dto.Research.Count));
            Assert.That(content.TryGetBuilding(new BuildingTypeId("house"), out BuildingDefinition? house), Is.True);
            Assert.That(house!.Kind, Is.EqualTo(BuildingKind.House));
            Assert.That(house.GetLevel(1).Recipe, Is.Null);
            Assert.That(content.TryGetBuilding(new BuildingTypeId("sawmill"), out BuildingDefinition? sawmill), Is.True);
            Assert.That(sawmill!.Kind, Is.EqualTo(BuildingKind.Producer));
            Assert.That(sawmill.GetLevel(1).Recipe!.Labor, Is.EqualTo(180L));
            Assert.That(sawmill.GetLevel(1).Recipe!.Inputs.Single().Quantity, Is.EqualTo(2L));
            Assert.That(sawmill.MaxLevel, Is.GreaterThan(1));
            Assert.That(sawmill.GetLevel(2).UpgradeSeconds, Is.GreaterThan(0L));
            Assert.That(content.TryGetBuilding(new BuildingTypeId("trading_post"), out BuildingDefinition? post), Is.True);
            Assert.That(post!.IsAvailableAtStart, Is.False);
            Assert.That(post.UnlockedByResearch!.Value.Value, Is.EqualTo("trade_charter"));
        }

        /// <summary>Every effect type the answer holds survives the mapping, which the factory proves by accepting the research that uses it.</summary>
        [Test]
        public async Task Load_TheRealAnswer_KeepsEveryEffectOfEveryResearch()
        {
            _transport.Answer(200, Fixtures.Read("content.json"));
            ContentResponseDto dto = new ApiJson().Deserialize<ContentResponseDto>(Fixtures.Read("content.json"))!;

            GameContent content = (await _service.LoadAsync(CancellationToken.None)).Content!;

            foreach (ContentResearchDto research in dto.Research)
            {
                Assert.That(content.TryGetResearch(new ResearchId(research.Id), out ResearchDefinition? definition), Is.True, research.Id);
                Assert.That(definition!.Effects, Has.Count.EqualTo(research.Effects.Count), research.Id);
                Assert.That(definition.Prerequisites.Select(p => p.Value), Is.EqualTo(research.Prerequisites), research.Id);
            }
        }

        /// <summary>The mapper writes the content file's spelling of a kind (<c>house</c>, <c>producer</c>), not the response's.</summary>
        [Test]
        public void Mapper_WritesTheContentFilesKindSpelling()
        {
            ContentResponseDto dto = new ApiJson().Deserialize<ContentResponseDto>(Fixtures.Read("content.json"))!;

            var data = ContentMapper.ToData(dto);

            Assert.That(data.Buildings!.Select(b => b!.Kind).Distinct().OrderBy(k => k), Is.EqualTo(new[] { "house", "producer" }));
            Assert.That(data.Buildings!.First(b => b!.Id == "house")!.Levels![0]!.Recipe, Is.Null);
            Assert.That(data.Buildings!.First(b => b!.Id == "sawmill")!.Levels![0]!.UpgradeCost, Is.Null);
        }

        /// <summary>Content the factory refuses is a result that lists every error with its path, not an exception and not a half-built content.</summary>
        [Test]
        public async Task Load_ContentWithRuleErrors_ListsEveryErrorAndHasNoContent()
        {
            string broken = Fixtures.Read("content.json").Replace("\"gridWidth\":8", "\"gridWidth\":0").Replace("\"gridHeight\":8", "\"gridHeight\":-1");
            _transport.Answer(200, broken);

            ContentLoadResult result = await _service.LoadAsync(CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Content, Is.Null);
            Assert.That(result.ApiError, Is.Null);
            Assert.That(result.IsInvalidContent, Is.True);
            Assert.That(result.ContentErrors.Select(error => error.Path), Is.EquivalentTo(new[] { "rules.gridWidth", "rules.gridHeight" }));
        }

        /// <summary>A building that points at an item the answer does not define is refused with a reference error naming where.</summary>
        [Test]
        public async Task Load_ContentWithAnUnknownItem_IsInvalid()
        {
            string broken = Fixtures.Read("content.json").Replace("\"cost\":{\"coins\":150,\"logs\":30}", "\"cost\":{\"coins\":150,\"gold\":30}");
            _transport.Answer(200, broken);

            ContentLoadResult result = await _service.LoadAsync(CancellationToken.None);

            Assert.That(result.IsInvalidContent, Is.True);
            Assert.That(result.ContentErrors.Any(error => error.Path.Contains("sawmill")), Is.True, string.Join("; ", result.ContentErrors));
        }

        /// <summary>A refusal by the server is a failed request with its code; the token being refused is how an expired login shows up.</summary>
        [Test]
        public async Task Load_Refused_IsAFailedRequest_WithTheServersCode()
        {
            _transport.Answer(401, Fixtures.Read("error-401-AUTHENTICATION_REQUIRED.json"));

            ContentLoadResult result = await _service.LoadAsync(CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.IsInvalidContent, Is.False);
            Assert.That(result.ApiError!.Code, Is.EqualTo(ApiErrorCodes.AuthenticationRequired));
        }

        /// <summary>A lost connection and a proxy's HTML page are failed requests with the client codes.</summary>
        [Test]
        public async Task Load_WithoutAConnectionOrWithAProxyPage_IsAFailedRequest()
        {
            _transport.Fail("down").Answer(502, "<html>Bad Gateway</html>");

            ContentLoadResult lost = await _service.LoadAsync(CancellationToken.None);
            ContentLoadResult proxy = await _service.LoadAsync(CancellationToken.None);

            Assert.That(lost.ApiError!.Code, Is.EqualTo(ApiErrorCodes.ClientNetworkError));
            Assert.That(proxy.ApiError!.Code, Is.EqualTo(ApiErrorCodes.ClientInvalidResponse));
        }

        /// <summary>A 200 answer that lacks required members is not content: it fails as an invalid response before the factory sees it.</summary>
        [Test]
        public async Task Load_AnAnswerWithoutRules_IsAnInvalidResponse()
        {
            _transport.Answer(200, "{\"contentVersion\":\"x\"}");

            ContentLoadResult result = await _service.LoadAsync(CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ApiError!.Code, Is.EqualTo(ApiErrorCodes.ClientInvalidResponse));
        }

        /// <summary>A cancelled load throws, as every call of the client does.</summary>
        [Test]
        public void Load_Cancelled_Throws()
        {
            _transport.NeverAnswer();
            using (var cancel = new CancellationTokenSource())
            {
                Task<ContentLoadResult> load = _service.LoadAsync(cancel.Token);
                cancel.Cancel();

                Assert.That(async () => await load, Throws.InstanceOf<System.OperationCanceledException>());
            }
        }

        /// <summary>A failed request is neither a success nor invalid content, and carries no content errors.</summary>
        [Test]
        public void Result_RequestFailed_IsNeitherSuccessNorInvalidContent()
        {
            var error = new ApiError(ApiErrorCodes.ClientTimeout, 0, "late", null, null, null);

            ContentLoadResult failed = ContentLoadResult.RequestFailed(error);

            Assert.That(failed.IsSuccess, Is.False);
            Assert.That(failed.IsInvalidContent, Is.False);
            Assert.That(failed.ContentErrors, Is.Empty);
        }

        /// <summary>The errors of a result as text, for an assertion message.</summary>
        private static string Describe(ContentLoadResult result)
        {
            return result.ApiError != null ? result.ApiError.ToString() : string.Join("; ", result.ContentErrors.Select(error => error.ToString()));
        }
    }
}
