#nullable enable

using System;
using System.Collections.Generic;
using CityBuilder.Simulation;
using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The store fill of one building (visual v2, CR-0008-T2): an instance of the building level's fill model, whose children
    /// <c>Fill_&lt;item&gt;_&lt;step&gt;</c> are shown cumulatively, so step 2 shows steps 1 and 2. The building model itself is the empty store,
    /// and changing the fill never touches the moving parts, so the building's motion goes on while its store fills. Made once with its body;
    /// <see cref="Show"/> allocates nothing. Display only.
    /// </summary>
    public sealed class FillView
    {
        /// <summary>The name every step object starts with.</summary>
        public const string StepPrefix = "Fill_";

        /// <summary>The items the model has steps for, in the order first met.</summary>
        private readonly ItemId[] _items;

        /// <summary>How many steps each item of <see cref="_items"/> has.</summary>
        private readonly int[] _stepCounts;

        /// <summary>The step each item of <see cref="_items"/> shows now.</summary>
        private readonly int[] _shown;

        /// <summary>The step objects: for each item of <see cref="_items"/>, its steps in order (index = step - 1).</summary>
        private readonly GameObject[][] _steps;

        /// <summary>Reads the steps of a fill model instance and hides them all.</summary>
        /// <param name="fillRoot">The instance of a fill model.</param>
        /// <exception cref="ArgumentNullException"><paramref name="fillRoot"/> is null.</exception>
        public FillView(Transform fillRoot)
        {
            Root = fillRoot ?? throw new ArgumentNullException(nameof(fillRoot));
            var items = new List<string>();
            var steps = new List<List<GameObject>>();
            for (int i = 0; i < fillRoot.childCount; i++)
            {
                Transform child = fillRoot.GetChild(i);
                if (!TryParse(child.name, out string item, out int step))
                {
                    continue;
                }

                int index = items.IndexOf(item);
                if (index < 0)
                {
                    index = items.Count;
                    items.Add(item);
                    steps.Add(new List<GameObject>());
                }

                List<GameObject> list = steps[index];
                while (list.Count < step)
                {
                    list.Add(null!);
                }

                list[step - 1] = child.gameObject;
                child.gameObject.SetActive(false);
            }

            _items = new ItemId[items.Count];
            _stepCounts = new int[items.Count];
            _shown = new int[items.Count];
            _steps = new GameObject[items.Count][];
            for (int i = 0; i < items.Count; i++)
            {
                _items[i] = new ItemId(items[i]);
                _steps[i] = steps[i].ToArray();
                _stepCounts[i] = _steps[i].Length;
            }
        }

        /// <summary>The instance of the fill model.</summary>
        public Transform Root { get; }

        /// <summary>The number of items the model has steps for.</summary>
        public int ItemCount => _items.Length;

        /// <summary>An item the model has steps for.</summary>
        /// <param name="index">0 to <see cref="ItemCount"/> - 1.</param>
        /// <returns>The item.</returns>
        public ItemId ItemAt(int index)
        {
            return _items[index];
        }

        /// <summary>How many steps an item has.</summary>
        /// <param name="index">0 to <see cref="ItemCount"/> - 1.</param>
        /// <returns>1 to 3.</returns>
        public int StepCountAt(int index)
        {
            return _stepCounts[index];
        }

        /// <summary>The step an item shows now.</summary>
        /// <param name="index">0 to <see cref="ItemCount"/> - 1.</param>
        /// <returns>0 to its step count.</returns>
        public int ShownAt(int index)
        {
            return _shown[index];
        }

        /// <summary>Shows the first <paramref name="step"/> steps of an item and hides the rest; does nothing when that is shown already.</summary>
        /// <param name="index">0 to <see cref="ItemCount"/> - 1.</param>
        /// <param name="step">0 to the item's step count (clamped).</param>
        public void Show(int index, int step)
        {
            int clamped = Math.Max(0, Math.Min(step, _stepCounts[index]));
            if (_shown[index] == clamped)
            {
                return;
            }

            _shown[index] = clamped;
            GameObject[] objects = _steps[index];
            for (int s = 0; s < objects.Length; s++)
            {
                if (objects[s] != null)
                {
                    objects[s].SetActive(s < clamped);
                }
            }
        }

        /// <summary>Reads <c>Fill_&lt;item&gt;_&lt;step&gt;</c>; the item may hold underscores, the step is the number after the last one.</summary>
        private static bool TryParse(string name, out string item, out int step)
        {
            item = string.Empty;
            step = 0;
            if (!name.StartsWith(StepPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            int cut = name.LastIndexOf('_');
            if (cut <= StepPrefix.Length || !int.TryParse(name.Substring(cut + 1), out step) || step < 1 || step > StorageFill.MaxSteps)
            {
                return false;
            }

            item = name.Substring(StepPrefix.Length, cut - StepPrefix.Length);
            return true;
        }
    }
}
