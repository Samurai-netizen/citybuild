#nullable enable

using System;
using System.Collections.Generic;

namespace CityBuilder.Client.Presentation.Ui
{
    /// <summary>
    /// The menus the player opens and closes (the build menu's sheet, the building inspector, and every later one such as the research screen)
    /// as one group (owner directive OD-16): when one is opened, every other one closes. A menu joins with <see cref="Join"/>, giving the action
    /// that closes it, and tells the group with <see cref="Member.NotifyOpened"/> when it goes from closed to open. The group knows nothing
    /// else about a menu, so a new menu needs no change here. A close action must be safe to call while its menu is already closed. Pure
    /// code; no Unity objects.
    /// </summary>
    public sealed class MenuGroup
    {
        /// <summary>The members, in the order they joined.</summary>
        private readonly List<Member> _members = new List<Member>();

        /// <summary>The number of menus in the group.</summary>
        public int Count => _members.Count;

        /// <summary>Adds a menu to the group.</summary>
        /// <param name="close">Closes the menu; called when another menu opens, also when this one is closed already.</param>
        /// <returns>The member the menu tells when it opens.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="close"/> is null.</exception>
        public Member Join(Action close)
        {
            var member = new Member(this, close ?? throw new ArgumentNullException(nameof(close)));
            _members.Add(member);
            return member;
        }

        /// <summary>One menu's place in a <see cref="MenuGroup"/>.</summary>
        public sealed class Member
        {
            /// <summary>The group the menu belongs to.</summary>
            private readonly MenuGroup _group;

            /// <summary>Closes the menu.</summary>
            private readonly Action _close;

            /// <summary>Makes the member; <see cref="MenuGroup.Join"/> is the way in.</summary>
            internal Member(MenuGroup group, Action close)
            {
                _group = group;
                _close = close;
            }

            /// <summary>The menu has just opened: every other menu of the group is closed. Calling it for a menu that was open already is harmless.</summary>
            public void NotifyOpened()
            {
                for (int index = 0; index < _group._members.Count; index++)
                {
                    Member other = _group._members[index];
                    if (!ReferenceEquals(other, this))
                    {
                        other._close();
                    }
                }
            }
        }
    }
}
