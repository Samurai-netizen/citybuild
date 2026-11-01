#nullable enable

using System;
using CityBuilder.Client.Presentation.Ui;
using NUnit.Framework;

namespace CityBuilder.Client.Tests.EditMode.Presentation.Ui
{
    /// <summary>The group of menus (owner directive OD-16): opening one menu closes every other one, whatever their number.</summary>
    public sealed class MenuGroupTests
    {
        /// <summary>How many times the first, second and third test menu were told to close.</summary>
        private int _closedFirst, _closedSecond, _closedThird;

        /// <summary>Before each test: the counters start at zero (NUnit uses one instance of the class for all its tests).</summary>
        [SetUp]
        public void ResetCounters()
        {
            _closedFirst = _closedSecond = _closedThird = 0;
        }

        /// <summary>Opening a menu closes the other one and not itself.</summary>
        [Test]
        public void OpeningOne_ClosesTheOther_NotItself()
        {
            var group = new MenuGroup();
            MenuGroup.Member first = group.Join(() => _closedFirst++);
            group.Join(() => _closedSecond++);

            first.NotifyOpened();

            Assert.That(_closedFirst, Is.EqualTo(0));
            Assert.That(_closedSecond, Is.EqualTo(1));
        }

        /// <summary>With three menus (the research screen joins later) opening any one closes the two others, in both directions.</summary>
        [Test]
        public void WithThreeMenus_OpeningAnyOne_ClosesTheOthers()
        {
            var group = new MenuGroup();
            MenuGroup.Member first = group.Join(() => _closedFirst++);
            MenuGroup.Member second = group.Join(() => _closedSecond++);
            MenuGroup.Member third = group.Join(() => _closedThird++);

            second.NotifyOpened();
            Assert.That(new[] { _closedFirst, _closedSecond, _closedThird }, Is.EqualTo(new[] { 1, 0, 1 }));

            third.NotifyOpened();
            Assert.That(new[] { _closedFirst, _closedSecond, _closedThird }, Is.EqualTo(new[] { 2, 1, 1 }));

            first.NotifyOpened();
            Assert.That(new[] { _closedFirst, _closedSecond, _closedThird }, Is.EqualTo(new[] { 2, 2, 2 }));
            Assert.That(group.Count, Is.EqualTo(3));
        }

        /// <summary>A menu alone in its group has nothing to close.</summary>
        [Test]
        public void ALoneMenu_ClosesNothing()
        {
            var group = new MenuGroup();
            MenuGroup.Member only = group.Join(() => _closedFirst++);

            only.NotifyOpened();

            Assert.That(_closedFirst, Is.EqualTo(0));
        }

        /// <summary>Menus of two groups do not close each other.</summary>
        [Test]
        public void TwoGroups_AreIndependent()
        {
            var one = new MenuGroup();
            var two = new MenuGroup();
            MenuGroup.Member inOne = one.Join(() => _closedFirst++);
            two.Join(() => _closedSecond++);

            inOne.NotifyOpened();

            Assert.That(_closedSecond, Is.EqualTo(0));
        }

        /// <summary>A missing close action is a programmer error.</summary>
        [Test]
        public void Join_NullClose_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MenuGroup().Join(null!));
        }
    }
}
