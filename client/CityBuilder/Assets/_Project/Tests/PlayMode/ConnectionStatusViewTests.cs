#nullable enable

using System.Collections;
using CityBuilder.Client.Presentation.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Tests.PlayMode
{
    /// <summary>
    /// The connection status on the screen (M0-48): the real <c>UI/ConnectionStatus</c> and <c>UI/FatalError</c> layouts. A normal status goes
    /// into the label, and a fatal one swaps in the fatal layout with its title and lines.
    /// </summary>
    public sealed class ConnectionStatusViewTests
    {
        /// <summary>The object the view is made under.</summary>
        private GameObject _parent = null!;

        /// <summary>The view under test.</summary>
        private ConnectionStatusView _view = null!;

        /// <summary>Before each test: a view; waits a frame for the document's tree.</summary>
        [UnitySetUp]
        public IEnumerator CreateView()
        {
            _parent = new GameObject("Test status parent");
            _view = ConnectionStatusView.Create(_parent.transform)!;
            yield return null;
        }

        /// <summary>After each test: destroys everything.</summary>
        [TearDown]
        public void Destroy()
        {
            Object.DestroyImmediate(_parent);
        }

        /// <summary>A status that is not fatal is the label's text.</summary>
        [Test]
        public void Show_ANormalStatus_WritesTheLabel()
        {
            _view.Show(ConnectionStatusModel.Connecting());

            Assert.That(_view.Root!.Q<Label>(ConnectionStatusView.StatusLabelName).text, Is.EqualTo("Connecting..."));
        }

        /// <summary>A fatal status replaces the layout: the title and the lines are shown and the status label is gone.</summary>
        [Test]
        public void Show_AFatalStatus_SwapsInTheFatalLayout_WithItsLines()
        {
            _view.Show(ConnectionStatusModel.BadSettings("no URL"));

            VisualElement root = _view.Root!;
            Assert.That(root.Q<Label>(ConnectionStatusView.StatusLabelName), Is.Null);
            Assert.That(root.Q<Label>(ConnectionStatusView.FatalTitleName).text, Is.EqualTo("Cannot start"));
            Assert.That(root.Q<Label>(ConnectionStatusView.FatalDetailsName).text, Does.Contain("no URL"));
        }
    }
}
