#nullable enable

using System.Collections;
using CityBuilder.Client.Presentation.Feedback;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Tests.PlayMode
{
    /// <summary>
    /// The toast screen in a running scene (M0-47): the real <c>UI/Toasts</c> layout over a <see cref="ToastQueue"/>. A toast shows as a label
    /// with its kind's class, a toast whose time is up goes without anyone calling the queue, a repeated one shows once, and no label takes the pointer.
    /// </summary>
    public sealed class ToastHostTests
    {
        /// <summary>The object the host is made under.</summary>
        private GameObject _parent = null!;

        /// <summary>The queue the host shows; its toasts stay 300 ms so a test can see one go.</summary>
        private ToastQueue _queue = null!;

        /// <summary>The host under test.</summary>
        private ToastHost _host = null!;

        /// <summary>Before each test: a host over a queue with short times; waits a frame for the document's tree.</summary>
        [UnitySetUp]
        public IEnumerator CreateHost()
        {
            _parent = new GameObject("Test toast parent");
            _queue = new ToastQueue(3, 300, 300);
            _host = ToastHost.Create(_parent.transform, _queue)!;
            yield return null;
        }

        /// <summary>After each test: destroys everything.</summary>
        [TearDown]
        public void Destroy()
        {
            Object.DestroyImmediate(_parent);
        }

        /// <summary>The label list of the host's tree.</summary>
        private VisualElement List()
        {
            return _host.Root!.Q<VisualElement>(ToastHost.ListName);
        }

        /// <summary>A success and an error toast show as two labels with their words and their kind's class, oldest on top.</summary>
        [UnityTest]
        public IEnumerator Toasts_ShowAsLabels_WithTheirKindsClass()
        {
            _queue.Show(ToastKind.Success, "Construction started", ToastHost.NowMilliseconds());
            _queue.Show(ToastKind.Error, "All builders are busy", ToastHost.NowMilliseconds());
            yield return null;

            Assert.That(List().childCount, Is.EqualTo(2));
            var first = (Label)List()[0];
            var second = (Label)List()[1];
            Assert.That(first.text, Is.EqualTo("Construction started"));
            Assert.That(first.ClassListContains(ToastHost.SuccessClass), Is.True);
            Assert.That(second.text, Is.EqualTo("All builders are busy"));
            Assert.That(second.ClassListContains(ToastHost.ErrorClass), Is.True);
        }

        /// <summary>A toast goes when its time is up, driven by the host's own frame update.</summary>
        [UnityTest]
        public IEnumerator AToast_GoesWhenItsTimeIsUp()
        {
            _queue.Show(ToastKind.Error, "No connection. Check your network and try again", ToastHost.NowMilliseconds());
            yield return null;
            Assert.That(_host.ShownCount, Is.EqualTo(1));

            float deadline = Time.realtimeSinceStartup + 5f;
            while (_host.ShownCount > 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(_host.ShownCount, Is.EqualTo(0));
            Assert.That(_queue.Visible, Is.Empty);
        }

        /// <summary>The same words shown five times in a row are one label.</summary>
        [UnityTest]
        public IEnumerator ARepeatedToast_IsShownOnce()
        {
            for (int i = 0; i < 5; i++)
            {
                _queue.Show(ToastKind.Error, "That tile is already taken", ToastHost.NowMilliseconds());
            }

            yield return null;

            Assert.That(_host.ShownCount, Is.EqualTo(1));
        }

        /// <summary>No toast takes the pointer, so a press under it still reaches the ground and the screens below.</summary>
        [UnityTest]
        public IEnumerator NoToast_TakesThePointer()
        {
            _queue.Show(ToastKind.Success, "Research started", ToastHost.NowMilliseconds());
            yield return null;

            Assert.That(List()[0].pickingMode, Is.EqualTo(PickingMode.Ignore));
            Assert.That(_host.Root!.Q<VisualElement>(ToastHost.RootName).pickingMode, Is.EqualTo(PickingMode.Ignore));
        }
    }
}
