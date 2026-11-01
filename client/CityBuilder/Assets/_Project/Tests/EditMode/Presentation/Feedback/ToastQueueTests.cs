#nullable enable

using System;
using CityBuilder.Client.Presentation.Feedback;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Feedback
{
    /// <summary>The toasts on the screen (M0-47): showing, expiry, the cap, and the toast that is not shown twice.</summary>
    public sealed class ToastQueueTests
    {
        /// <summary>The host calls <see cref="ToastQueue.Update"/> every frame: with nothing to take away it allocates nothing (M0-48). The second assert shows the check does see an allocation.</summary>
        [Test]
        public void Update_WithNothingToTakeAway_AllocatesNothing()
        {
            var queue = new ToastQueue();
            queue.Show(ToastKind.Success, "Construction started", 0);
            queue.Update(1);

            Assert.That(() => { queue.Update(1); }, Is.Not.AllocatingGCMemory());
            Assert.That(() => { GC.KeepAlive(new object()); }, Is.AllocatingGCMemory());
        }

        /// <summary>A toast shows from the moment it is made until its time is up, and errors stay longer than successes.</summary>
        [Test]
        public void AToast_StaysForItsKindsTime_ThenGoes()
        {
            var queue = new ToastQueue(3, 1000, 4000);
            queue.Show(ToastKind.Success, "Construction started", 0);
            queue.Show(ToastKind.Error, "All builders are busy", 0);

            Assert.That(queue.Update(999), Is.False);
            Assert.That(queue.Visible.Count, Is.EqualTo(2));
            Assert.That(queue.Update(1000), Is.True);
            Assert.That(queue.Visible.Count, Is.EqualTo(1));
            Assert.That(queue.Visible[0].Text, Is.EqualTo("All builders are busy"));
            Assert.That(queue.Update(4000), Is.True);
            Assert.That(queue.Visible, Is.Empty);
        }

        /// <summary>Toasts list oldest first, and each has its own id.</summary>
        [Test]
        public void Toasts_AreListedOldestFirst_WithOwnIds()
        {
            var queue = new ToastQueue();
            Toast first = queue.Show(ToastKind.Success, "a", 0);
            Toast second = queue.Show(ToastKind.Success, "b", 10);

            Assert.That(queue.Visible[0], Is.SameAs(first));
            Assert.That(queue.Visible[1], Is.SameAs(second));
            Assert.That(second.Id, Is.Not.EqualTo(first.Id));
        }

        /// <summary>A fourth toast pushes the oldest out when three show at once.</summary>
        [Test]
        public void AToastBeyondTheCap_PushesTheOldestOut()
        {
            var queue = new ToastQueue(3);
            for (int i = 1; i <= 4; i++)
            {
                queue.Show(ToastKind.Error, "m" + i, 0);
            }

            Assert.That(queue.Visible.Count, Is.EqualTo(3));
            Assert.That(queue.Visible[0].Text, Is.EqualTo("m2"));
            Assert.That(queue.Visible[2].Text, Is.EqualTo("m4"));
        }

        /// <summary>The same words and kind do not stack: the shown toast gets a new time and nothing is added.</summary>
        [Test]
        public void ASecondIdenticalToast_RenewsTheFirst()
        {
            var queue = new ToastQueue(3, 1000, 4000);
            Toast first = queue.Show(ToastKind.Error, "No connection", 0);
            int changes = 0;
            queue.Changed += () => changes++;

            Toast again = queue.Show(ToastKind.Error, "No connection", 3000);

            Assert.That(again, Is.SameAs(first));
            Assert.That(queue.Visible.Count, Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(0));
            queue.Update(5000);
            Assert.That(queue.Visible.Count, Is.EqualTo(1), "the renewed toast stays until 3000 + 4000");
            queue.Update(7000);
            Assert.That(queue.Visible, Is.Empty);
        }

        /// <summary>The same words as a different kind are a different toast.</summary>
        [Test]
        public void TheSameWordsOfAnotherKind_AreAnotherToast()
        {
            var queue = new ToastQueue();
            queue.Show(ToastKind.Success, "Done", 0);
            queue.Show(ToastKind.Error, "Done", 0);
            Assert.That(queue.Visible.Count, Is.EqualTo(2));
        }

        /// <summary><see cref="ToastQueue.Changed"/> fires for an added toast and an expiry, and not for an update that changes nothing.</summary>
        [Test]
        public void Changed_FiresOnlyWhenTheListChanges()
        {
            var queue = new ToastQueue(3, 1000, 1000);
            int changes = 0;
            queue.Changed += () => changes++;

            queue.Show(ToastKind.Success, "a", 0);
            queue.Update(500);
            Assert.That(changes, Is.EqualTo(1));
            queue.Update(1000);
            Assert.That(changes, Is.EqualTo(2));
            queue.Update(2000);
            Assert.That(changes, Is.EqualTo(2));
        }

        /// <summary>A toast without words, and a queue that could not show one, are mistakes of the caller.</summary>
        [Test]
        public void BadArguments_Throw()
        {
            Assert.Throws<ArgumentException>(() => new ToastQueue().Show(ToastKind.Success, string.Empty, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ToastQueue(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ToastQueue(3, 0, 1000));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ToastQueue(3, 1000, 0));
        }
    }
}
