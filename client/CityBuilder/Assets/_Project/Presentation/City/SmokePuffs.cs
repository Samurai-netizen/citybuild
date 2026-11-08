#nullable enable

using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// Chimney smoke (visual v2, CR-0008-T2): a few instances of the smoke puff model under a building's <c>Anchor_Smoke</c> that rise, drift,
    /// grow and shrink away one after the other while the building works. They are opaque blobs of the palette's smoke colour, so the city
    /// keeps its one material; nothing shows (the puffs are switched off) while the building does not work. Display only; allocates nothing
    /// after it is made.
    /// </summary>
    public sealed class SmokePuffs
    {
        /// <summary>How many puffs are in the air at once.</summary>
        private const int PuffCount = 3;

        /// <summary>How long one puff lives, in seconds.</summary>
        private const float LifeSeconds = 2.4f;

        /// <summary>How far a puff rises over its life, in world units.</summary>
        private const float RiseUnits = 0.34f;

        /// <summary>How far a puff drifts sideways over its life, in world units.</summary>
        private const float DriftUnits = 0.07f;

        /// <summary>A puff's size at the middle of its life, as a scale of the model.</summary>
        private const float PeakScale = 1.1f;

        /// <summary>The puffs.</summary>
        private readonly Transform[] _puffs = new Transform[PuffCount];

        /// <summary>The time the smoke has been rising, in seconds.</summary>
        private float _seconds;

        /// <summary>Whether the puffs are switched on.</summary>
        private bool _shown;

        /// <summary>Makes the puffs under <paramref name="anchor"/>, switched off.</summary>
        /// <param name="puffModel">The smoke puff model (<c>Models/common/fx_smoke_puff</c>).</param>
        /// <param name="anchor">Where the smoke leaves the chimney.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public SmokePuffs(GameObject puffModel, Transform anchor)
        {
            if (puffModel == null)
            {
                throw new ArgumentNullException(nameof(puffModel));
            }

            if (anchor == null)
            {
                throw new ArgumentNullException(nameof(anchor));
            }

            for (int i = 0; i < PuffCount; i++)
            {
                GameObject puff = Object.Instantiate(puffModel, anchor, false);
                puff.name = "Smoke " + i;
                puff.transform.localScale = Vector3.zero;
                puff.SetActive(false);
                _puffs[i] = puff.transform;
            }
        }

        /// <summary>Whether the puffs are switched on.</summary>
        public bool Shown => _shown;

        /// <summary>
        /// Moves the smoke on: each puff's place in its life gives its height, drift and size, all scaled by <paramref name="intensity"/>, so
        /// the smoke thins out when the building stops. At intensity 0 the puffs are switched off once.
        /// </summary>
        /// <param name="deltaSeconds">The frame time, in seconds.</param>
        /// <param name="intensity">How strongly the building works, 0 to 1.</param>
        public void Tick(float deltaSeconds, float intensity)
        {
            bool show = intensity > 0f;
            if (show != _shown)
            {
                _shown = show;
                for (int i = 0; i < PuffCount; i++)
                {
                    _puffs[i].gameObject.SetActive(show);
                }
            }

            if (!show)
            {
                return;
            }

            _seconds += deltaSeconds;
            for (int i = 0; i < PuffCount; i++)
            {
                float life = _seconds / LifeSeconds + (float)i / PuffCount;
                float t = life - Mathf.Floor(life);
                float size = PeakScale * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.8f) * intensity;
                Transform puff = _puffs[i];
                puff.localPosition = new Vector3(DriftUnits * t * (i % 2 == 0 ? 1f : -0.6f), RiseUnits * t, DriftUnits * 0.5f * t);
                puff.localScale = new Vector3(size, size, size);
            }
        }
    }
}
