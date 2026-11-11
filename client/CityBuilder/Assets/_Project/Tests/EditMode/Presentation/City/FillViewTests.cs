#nullable enable

using CityBuilder.Client.Presentation.City;
using CityBuilder.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>
    /// The store fill of a building (<see cref="FillView"/>) on a hand-made fill model: steps are read by name, start hidden, show cumulatively
    /// per item, and other children are ignored.
    /// </summary>
    public sealed class FillViewTests
    {
        /// <summary>The fill model each test builds.</summary>
        private GameObject? _root;

        /// <summary>Destroys the fill model.</summary>
        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        /// <summary>A fill model like the Sawmill's: planks in three steps, logs in two (out of order), and a stray child.</summary>
        private FillView Sawmill()
        {
            _root = new GameObject("sawmill_l1_fill");
            foreach (string name in new[] { "Fill_planks_1", "Fill_logs_2", "Fill_planks_3", "Fill_logs_1", "Fill_planks_2", "Pile" })
            {
                new GameObject(name).transform.SetParent(_root.transform, false);
            }

            return new FillView(_root.transform);
        }

        /// <summary>The model's items and step counts are read from the names; every step starts hidden.</summary>
        [Test]
        public void TheSteps_AreReadByName_AndStartHidden()
        {
            FillView fill = Sawmill();
            Assert.That(fill.ItemCount, Is.EqualTo(2));
            Assert.That(fill.ItemAt(0), Is.EqualTo(new ItemId("planks")));
            Assert.That(fill.StepCountAt(0), Is.EqualTo(3));
            Assert.That(fill.ItemAt(1), Is.EqualTo(new ItemId("logs")));
            Assert.That(fill.StepCountAt(1), Is.EqualTo(2));
            for (int i = 0; i < _root!.transform.childCount; i++)
            {
                Transform child = _root.transform.GetChild(i);
                Assert.That(child.gameObject.activeSelf, Is.EqualTo(child.name == "Pile"), child.name);
            }
        }

        /// <summary>Step 2 shows steps 1 and 2 of that item only; going down hides the higher steps again.</summary>
        [Test]
        public void Show_IsCumulative_PerItem()
        {
            FillView fill = Sawmill();
            fill.Show(0, 2);
            Transform root = _root!.transform;
            Assert.That(root.Find("Fill_planks_1").gameObject.activeSelf, Is.True);
            Assert.That(root.Find("Fill_planks_2").gameObject.activeSelf, Is.True);
            Assert.That(root.Find("Fill_planks_3").gameObject.activeSelf, Is.False);
            Assert.That(root.Find("Fill_logs_1").gameObject.activeSelf, Is.False);
            fill.Show(0, 1);
            Assert.That(root.Find("Fill_planks_2").gameObject.activeSelf, Is.False);
            Assert.That(fill.ShownAt(0), Is.EqualTo(1));
            fill.Show(1, 9);
            Assert.That(fill.ShownAt(1), Is.EqualTo(2));
        }
    }
}
