#nullable enable

using CityBuilder.Client.Presentation.City;
using NUnit.Framework;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>
    /// The moving parts of a model (<see cref="MotionRig"/>), on a hand-made hierarchy like an imported model: a part and its sibling
    /// <c>_Pose</c> empty are found, the part moves between its rest and its pose, eases with the intensity and goes back to rest.
    /// </summary>
    public sealed class MotionRigTests
    {
        /// <summary>The tolerance for positions and angles.</summary>
        private const float Tolerance = 1e-3f;

        /// <summary>The root of the hierarchy each test builds.</summary>
        private GameObject? _root;

        /// <summary>Destroys the hierarchy.</summary>
        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        /// <summary>Builds a model root with one part named <paramref name="name"/> at (1, 0, 0) and its pose turned and moved.</summary>
        private Transform Model(string name, Vector3 poseEuler, Vector3 poseOffset)
        {
            _root = new GameObject("model");
            var part = new GameObject(name);
            part.transform.SetParent(_root.transform, false);
            part.transform.localPosition = new Vector3(1f, 0f, 0f);
            var pose = new GameObject(name + PartMotions.PoseSuffix);
            pose.transform.SetParent(_root.transform, false);
            pose.transform.localPosition = part.transform.localPosition + poseOffset;
            pose.transform.localRotation = Quaternion.Euler(poseEuler);
            return _root.transform;
        }

        /// <summary>A model without moving parts has no rig; a part without its pose, or a name without a row, is not a moving part.</summary>
        [Test]
        public void Find_IgnoresAnythingThatIsNotAMovingPart()
        {
            Transform model = Model("Chimney", Vector3.zero, Vector3.up);
            Assert.That(MotionRig.Find(model), Is.Null);
            var lonely = new GameObject("Axe");
            lonely.transform.SetParent(model, false);
            Assert.That(MotionRig.Find(model), Is.Null);
        }

        /// <summary>A ping-pong part (a hoist) glides to its pose's position in the middle of its cycle and is back at rest after a whole cycle.</summary>
        [Test]
        public void APingPongPart_GlidesToItsPoseAndBack()
        {
            MotionRig rig = MotionRig.Find(Model("Hoist", Vector3.zero, new Vector3(0f, 0.1f, 0f)))!;
            Assert.That(rig.Count, Is.EqualTo(1));
            rig.Advance(2.5f, 1f);
            Assert.That(Vector3.Distance(rig.PartAt(0).localPosition, new Vector3(1f, 0.1f, 0f)), Is.LessThan(Tolerance));
            rig.Advance(2.5f, 1f);
            Assert.That(Vector3.Distance(rig.PartAt(0).localPosition, new Vector3(1f, 0f, 0f)), Is.LessThan(Tolerance));
        }

        /// <summary>A chop part (an axe) turns as far as its pose at the top of its swing, around the axis between them.</summary>
        [Test]
        public void AChopPart_TurnsToItsPose()
        {
            MotionRig rig = MotionRig.Find(Model("Axe", new Vector3(0f, 0f, 100f), Vector3.zero))!;
            rig.Advance(1.4f * 0.62f, 1f);
            Assert.That(Quaternion.Angle(rig.PartAt(0).localRotation, Quaternion.Euler(0f, 0f, 100f)), Is.LessThan(0.1f));
        }

        /// <summary>A spin part (a wheel) turns about its pose's axis and keeps turning: after a quarter of its cycle it is a quarter turn round.</summary>
        [Test]
        public void ASpinPart_TurnsAboutItsPoseAxis()
        {
            MotionRig rig = MotionRig.Find(Model(PartMotions.Wheel, new Vector3(90f, 0f, 0f), Vector3.zero))!;
            rig.Advance(1f, 1f);
            Assert.That(Quaternion.Angle(rig.PartAt(0).localRotation, Quaternion.Euler(90f, 0f, 0f)), Is.LessThan(0.1f));
            rig.Advance(1f, 1f);
            Assert.That(Quaternion.Angle(rig.PartAt(0).localRotation, Quaternion.Euler(180f, 0f, 0f)), Is.LessThan(0.1f));
            Assert.That(Vector3.Distance(rig.PartAt(0).localPosition, new Vector3(1f, 0f, 0f)), Is.LessThan(Tolerance));
        }

        /// <summary>At half intensity the time runs at half speed and the part moves half as far; Rest puts it back.</summary>
        [Test]
        public void Intensity_SlowsAndShrinksTheMotion_AndRestPutsThePartBack()
        {
            MotionRig rig = MotionRig.Find(Model("Hoist", Vector3.zero, new Vector3(0f, 0.2f, 0f)))!;
            rig.Advance(5f, 0.5f);
            Assert.That(rig.Seconds, Is.EqualTo(2.5f).Within(Tolerance));
            Assert.That(rig.PartAt(0).localPosition.y, Is.EqualTo(0.1f).Within(Tolerance));
            rig.Rest();
            Assert.That(rig.Seconds, Is.EqualTo(0f));
            Assert.That(rig.PartAt(0).localPosition, Is.EqualTo(new Vector3(1f, 0f, 0f)));
        }
    }
}
