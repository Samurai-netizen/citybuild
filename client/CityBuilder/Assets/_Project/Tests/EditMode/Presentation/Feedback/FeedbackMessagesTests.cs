#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using CityBuilder.Client.Net;
using CityBuilder.Client.Presentation.Feedback;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Feedback
{
    /// <summary>The words of every result (M0-47): no error code is without a sentence, and no sentence shows a code, a server text or a stack.</summary>
    public sealed class FeedbackMessagesTests
    {
        /// <summary>The longest sentence a toast should carry on a phone screen, in characters.</summary>
        private const int LongestMessage = 64;

        /// <summary>Every code the client can meet: the constants of <see cref="ApiErrorCodes"/> by reflection, plus the flow's own code.</summary>
        private static IEnumerable<string> AllCodes()
        {
            var codes = new List<string>();
            foreach (FieldInfo field in typeof(ApiErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.IsLiteral && field.FieldType == typeof(string))
                {
                    codes.Add((string)field.GetRawConstantValue());
                }
            }

            codes.Add(FeedbackMessages.CommandFailedCode);
            return codes;
        }

        /// <summary>The check behind the whole table: a code added to <see cref="ApiErrorCodes"/> without a sentence fails here.</summary>
        [Test]
        public void EveryErrorCode_HasAMessage()
        {
            var missing = new List<string>();
            foreach (string code in AllCodes())
            {
                if (!FeedbackMessages.TryGetErrorMessage(code, out string message) || message.Length == 0)
                {
                    missing.Add(code);
                }
            }

            Assert.That(missing, Is.Empty, "These error codes have no message in FeedbackMessages: " + string.Join(", ", missing));
        }

        /// <summary>The list really covers the class: the reflection found the server's catalogue and the client's own codes.</summary>
        [Test]
        public void TheCodeList_IncludesTheServerAndTheClientCodes()
        {
            var codes = new List<string>(AllCodes());
            Assert.That(codes, Does.Contain(ApiErrorCodes.NoFreeBuilder));
            Assert.That(codes, Does.Contain(ApiErrorCodes.ClientTimeout));
            Assert.That(codes, Does.Contain(FeedbackMessages.CommandFailedCode));
            Assert.That(codes.Count, Is.GreaterThanOrEqualTo(27));
        }

        /// <summary>A sentence is short, ends the way a toast reads, and never shows a code, an exception or a stack frame.</summary>
        [Test]
        public void EveryMessage_IsShortPlainWords()
        {
            foreach (string code in AllCodes())
            {
                FeedbackMessages.TryGetErrorMessage(code, out string message);
                Assert.That(message.Length, Is.LessThanOrEqualTo(LongestMessage), code);
                Assert.That(Regex.IsMatch(message, "[A-Z]{2,}_[A-Z]"), Is.False, code + " shows a code: " + message);
                Assert.That(message, Does.Not.Contain("Exception"), code);
                Assert.That(Regex.IsMatch(message, @"\bat [\w.]+\("), Is.False, code + " shows a stack frame: " + message);
                Assert.That(message, Does.Not.Contain("http"), code);
                Assert.That(message, Does.Not.Contain("\n"), code);
            }
        }

        /// <summary>The three examples of the task spec, as the table words them (decision 2: no builder count, no building name).</summary>
        [TestCase(ApiErrorCodes.NoFreeBuilder, "All builders are busy")]
        [TestCase(ApiErrorCodes.BuildingLocked, "Research needed first")]
        [TestCase(ApiErrorCodes.InsufficientWorkers, "Not enough free workers. Build more housing")]
        public void TheSpecExamples_AreWordedAsLogged(string code, string expected)
        {
            Assert.That(FeedbackMessages.ForError(Error(code)), Is.EqualTo(expected));
        }

        /// <summary>A lost connection and a timeout tell the player to try again; they are not refusals.</summary>
        [TestCase(ApiErrorCodes.ClientNetworkError)]
        [TestCase(ApiErrorCodes.ClientTimeout)]
        [TestCase(ApiErrorCodes.ClientInvalidResponse)]
        [TestCase(ApiErrorCodes.InternalError)]
        [TestCase(ApiErrorCodes.ConcurrencyConflict)]
        public void ARetryableFailure_SaysToTryAgain(string code)
        {
            Assert.That(FeedbackMessages.ForError(Error(code)), Does.Contain("Try again").Or.Contain("try again"));
        }

        /// <summary>The server's own sentence is for logs: it never reaches the player, whatever it holds.</summary>
        [Test]
        public void TheServersDetail_IsNeverShown()
        {
            var error = new ApiError(ApiErrorCodes.TileOccupied, 409, "System.InvalidOperationException at Foo.Bar()", "00-trace", null, null);
            Assert.That(FeedbackMessages.ForError(error), Is.EqualTo("That tile is already taken"));
        }

        /// <summary>A rate limit names the wait the server asked for; without one it asks for a moment.</summary>
        [Test]
        public void ARateLimit_NamesTheWait_WhenTheServerGaveOne()
        {
            var waiting = new ApiError(ApiErrorCodes.RateLimited, 429, "x", null, null, 7);
            var unknown = new ApiError(ApiErrorCodes.RateLimited, 429, "x", null, null, null);
            Assert.That(FeedbackMessages.ForError(waiting), Is.EqualTo("Too many requests. Wait 7 s"));
            Assert.That(FeedbackMessages.ForError(unknown), Is.EqualTo("Too many requests. Wait a moment"));
        }

        /// <summary>A code the table lacks (a newer server) still gets a plain sentence, never the code.</summary>
        [Test]
        public void AnUnknownCode_GetsThePlainFallback()
        {
            string text = FeedbackMessages.ForError(Error("SOMETHING_NEW"));
            Assert.That(text, Is.EqualTo(FeedbackMessages.UnknownErrorMessage));
            Assert.That(FeedbackMessages.TryGetErrorMessage("SOMETHING_NEW", out _), Is.False);
        }

        /// <summary>Each of the four commands has its own success sentence, and a command added later cannot be left without one.</summary>
        [Test]
        public void EveryCommandKind_HasASuccessMessage()
        {
            var seen = new HashSet<string>();
            foreach (CommandKind kind in Enum.GetValues(typeof(CommandKind)))
            {
                string text = FeedbackMessages.ForSuccess(kind);
                Assert.That(text, Is.Not.Empty, kind.ToString());
                Assert.That(text, Is.Not.EqualTo("Done"), kind + " has only the fallback sentence");
                Assert.That(seen.Add(text), Is.True, kind + " repeats a sentence");
            }
        }

        /// <summary>An error with the given code and nothing else.</summary>
        private static ApiError Error(string code)
        {
            return new ApiError(code, 0, "detail", null, null, null);
        }
    }
}
