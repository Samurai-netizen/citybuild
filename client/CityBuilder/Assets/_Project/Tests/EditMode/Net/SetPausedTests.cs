#nullable enable

using System;
using CityBuilder.Client.Net;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Net
{
    /// <summary>The <c>set-paused</c> command on the client (CR-0008 amendment 2): its body and its intent.</summary>
    public sealed class SetPausedTests
    {
        /// <summary>An id used by every test.</summary>
        private static readonly Guid RequestId = new Guid("6f1c0000-0000-4000-8000-000000000003");

        /// <summary>The body has the request id, the building and the flag, and nothing else.</summary>
        [Test]
        public void Body_HasTheBuildingAndTheFlagOnly()
        {
            string text = new ApiJson().Serialize(new SetPausedRequestDto(RequestId, 4, true));

            Assert.That(text, Is.EqualTo("{\"requestId\":\"6f1c0000-0000-4000-8000-000000000003\",\"buildingId\":4,\"paused\":true}"));
        }

        /// <summary>A pause and a resume of one building are different intents; each prints as a call and carries its flag.</summary>
        [Test]
        public void Intent_PauseAndResumeDiffer()
        {
            CommandIntent pause = CommandIntent.SetPaused(4, true);
            CommandIntent resume = CommandIntent.SetPaused(4, false);

            Assert.That(pause, Is.EqualTo(CommandIntent.SetPaused(4, true)));
            Assert.That(pause, Is.Not.EqualTo(resume));
            Assert.That((pause.Paused, resume.Paused), Is.EqualTo((true, false)));
            Assert.That(pause.ToString(), Is.EqualTo("SetPaused(building 4, pause)"));
            Assert.That(resume.ToString(), Is.EqualTo("SetPaused(building 4, resume)"));
        }
    }
}
