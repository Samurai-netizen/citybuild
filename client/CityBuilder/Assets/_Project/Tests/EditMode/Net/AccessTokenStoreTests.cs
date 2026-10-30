#nullable enable

using CityBuilder.Client.Net;
using NUnit.Framework;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>
    /// The token stores (M0-38): <c>PlayerPrefs</c> for development builds, nothing for the others. These tests use their own key prefix and
    /// remove its keys, so they never touch the login the Editor keeps for a real Play Mode run.
    /// </summary>
    public sealed class AccessTokenStoreTests
    {
        /// <summary>The prefix of the keys these tests use.</summary>
        private const string TestPrefix = "CityBuilder.Tests.DevLogin.";

        /// <summary>The store under test.</summary>
        private PlayerPrefsAccessTokenStore _store = null!;

        /// <summary>Before each test: a store over the test prefix with no keys.</summary>
        [SetUp]
        public void CreateStore()
        {
            _store = new PlayerPrefsAccessTokenStore(TestPrefix);
            _store.Clear();
        }

        /// <summary>After each test: removes the test keys.</summary>
        [TearDown]
        public void RemoveKeys()
        {
            _store.Clear();
        }

        /// <summary>A saved login loads back as the same name and token.</summary>
        [Test]
        public void PlayerPrefsStore_SaveThenLoad_ReturnsTheLogin()
        {
            _store.Save(new StoredLogin("dev1", "tok-1"));

            StoredLogin? loaded = new PlayerPrefsAccessTokenStore(TestPrefix).Load();

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded!.DisplayName, Is.EqualTo("dev1"));
            Assert.That(loaded.AccessToken, Is.EqualTo("tok-1"));
        }

        /// <summary>The keys are named for what they hold, under one prefix; nothing else is written.</summary>
        [Test]
        public void PlayerPrefsStore_Save_WritesTwoNamedKeys()
        {
            _store.Save(new StoredLogin("dev1", "tok-1"));

            Assert.That(PlayerPrefs.GetString(TestPrefix + "AccessToken"), Is.EqualTo("tok-1"));
            Assert.That(PlayerPrefs.GetString(TestPrefix + "DisplayName"), Is.EqualTo("dev1"));
            Assert.That(PlayerPrefsAccessTokenStore.DefaultKeyPrefix, Is.EqualTo("CityBuilder.DevLogin."));
        }

        /// <summary>A store with nothing saved loads null.</summary>
        [Test]
        public void PlayerPrefsStore_WithNothingSaved_LoadsNull()
        {
            Assert.That(_store.Load(), Is.Null);
        }

        /// <summary>A half-written pair (a token without its name) is treated as nothing.</summary>
        [Test]
        public void PlayerPrefsStore_WithOnlyAToken_LoadsNull()
        {
            PlayerPrefs.SetString(TestPrefix + "AccessToken", "tok-1");

            Assert.That(_store.Load(), Is.Null);
        }

        /// <summary>Clearing removes both keys.</summary>
        [Test]
        public void PlayerPrefsStore_Clear_ForgetsTheLogin()
        {
            _store.Save(new StoredLogin("dev1", "tok-1"));

            _store.Clear();

            Assert.That(_store.Load(), Is.Null);
            Assert.That(PlayerPrefs.HasKey(TestPrefix + "AccessToken"), Is.False);
            Assert.That(PlayerPrefs.HasKey(TestPrefix + "DisplayName"), Is.False);
        }

        /// <summary>A development build gets the <c>PlayerPrefs</c> store.</summary>
        [Test]
        public void For_ADevelopmentBuild_IsThePlayerPrefsStore()
        {
            Assert.That(AccessTokenStores.For(true), Is.TypeOf<PlayerPrefsAccessTokenStore>());
        }

        /// <summary>Any other build gets a store that keeps nothing: it saves nothing and loads nothing.</summary>
        [Test]
        public void For_AReleaseBuild_KeepsNothing()
        {
            IAccessTokenStore store = AccessTokenStores.For(false);

            store.Save(new StoredLogin("dev1", "tok-1"));

            Assert.That(store, Is.TypeOf<NullAccessTokenStore>());
            Assert.That(store.Load(), Is.Null);
        }
    }
}
