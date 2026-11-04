#nullable enable

using CityBuilder.Client.Presentation.City;
using NUnit.Framework;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Presentation.City
{
    /// <summary>
    /// The animator of a building (<see cref="BuildingAnimator"/>), driven by <see cref="BuildingAnimator.Step"/>: the motion eases in when the
    /// building starts working, settles at rest when it stops, and a building at rest does nothing.
    /// </summary>
    public sealed class BuildingAnimatorTests
    {
        /// <summary>The objects each test builds.</summary>
        private GameObject? _root;

        /// <summary>Destroys the objects.</summary>
        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        /// <summary>An animator on a building whose body has a hoist that rises 0.1 at its pose.</summary>
        private BuildingAnimator Building(out Transform hoist)
        {
            _root = new GameObject("Building 1 warehouse");
            var body = new GameObject("Body");
            body.transform.SetParent(_root.transform, false);
            hoist = new GameObject("Hoist").transform;
            hoist.SetParent(body.transform, false);
            var pose = new GameObject("Hoist" + PartMotions.PoseSuffix);
            pose.transform.SetParent(body.transform, false);
            pose.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            var animator = _root.AddComponent<BuildingAnimator>();
            animator.Bind(MotionRig.Find(body.transform), null);
            return animator;
        }

        /// <summary>Working, the intensity rises over the ease time and the part moves; stopped, it falls back to 0 and the part is at rest.</summary>
        [Test]
        public void TheMotion_EasesIn_AndSettlesAtRest()
        {
            BuildingAnimator animator = Building(out Transform hoist);
            animator.Working = true;
            animator.Step(BuildingAnimator.EaseSeconds / 2f);
            Assert.That(animator.Intensity, Is.EqualTo(0.5f).Within(1e-4f));
            for (int i = 0; i < 20; i++)
            {
                animator.Step(0.1f);
            }

            Assert.That(animator.Intensity, Is.EqualTo(1f));
            Assert.That(hoist.localPosition.y, Is.GreaterThan(0f));
            animator.Working = false;
            for (int i = 0; i < 10; i++)
            {
                animator.Step(0.1f);
            }

            Assert.That(animator.Intensity, Is.EqualTo(0f));
            Assert.That(hoist.localPosition, Is.EqualTo(Vector3.zero));
        }

        /// <summary>A building that never works never moves.</summary>
        [Test]
        public void ABuildingThatDoesNotWork_StaysAtRest()
        {
            BuildingAnimator animator = Building(out Transform hoist);
            animator.Step(1f);
            Assert.That(animator.Intensity, Is.EqualTo(0f));
            Assert.That(hoist.localPosition, Is.EqualTo(Vector3.zero));
        }
    }
}
