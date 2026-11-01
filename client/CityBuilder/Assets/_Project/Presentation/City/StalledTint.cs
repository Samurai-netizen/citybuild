#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The slightly washed-out look of a stalled building (M0-50): a property block on each renderer of the building's body, so the art's one
    /// shared material is never copied (<c>09</c> rule 3, <c>art/README.md</c>). A model's colours come from the palette texture, which a
    /// multiplied colour can only darken, so the block mixes the look toward grey the way a lerp would: the base colour is scaled down by
    /// <see cref="Mix"/> and a grey of the same share is added as emission (the shared material has <c>_EMISSION</c> on, with black emission, for
    /// this). A body of primitives has no texture and no emission, so its colour is lerped toward its own grey instead. The state is kept on the
    /// <see cref="BuildingView"/>; the block and the renderer list are reused, so a change of look allocates nothing once they have grown. Display
    /// only; made by <see cref="CityView"/>.
    /// </summary>
    public sealed class StalledTint
    {
        /// <summary>How much of the look is mixed toward grey, 0 (none) to 1 (all grey): slight.</summary>
        public const float Mix = 0.35f;

        /// <summary>The brightness of the grey the look is mixed toward.</summary>
        public const float Grey = 0.55f;

        /// <summary>The shader property of the base colour (URP Lit).</summary>
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>The shader property of the emission colour (URP Lit).</summary>
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>The shader property of the base texture (URP Lit); a model has one, a primitive has none.</summary>
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        /// <summary>The block written to each renderer, reused.</summary>
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        /// <summary>The renderers of one body, reused.</summary>
        private readonly List<Renderer> _renderers = new List<Renderer>();

        /// <summary>
        /// Makes <paramref name="view"/>'s body look stalled or normal. Does nothing when the body already looks that way; a new body (a new
        /// level or the construction site) starts normal, so a stalled building gets the look again on the next call.
        /// </summary>
        /// <param name="view">The building's view.</param>
        /// <param name="stalled">Whether the building is stalled.</param>
        public void Apply(BuildingView view, bool stalled)
        {
            Transform? body = view.Body;
            if (body == null)
            {
                return;
            }

            // Reference equality: a destroyed body still compares equal to null under Unity's operator.
            if (!ReferenceEquals(view.TintedBody, body))
            {
                view.TintedBody = body;
                view.Stalled = false;
            }

            if (view.Stalled == stalled)
            {
                return;
            }

            body.GetComponentsInChildren(true, _renderers);
            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer renderer = _renderers[i];
                if (stalled)
                {
                    renderer.SetPropertyBlock(BlockFor(renderer));
                }
                else
                {
                    renderer.SetPropertyBlock(null);
                }
            }

            _renderers.Clear();
            view.Stalled = stalled;
        }

        /// <summary>The block that washes out <paramref name="renderer"/>'s look, written into the shared block.</summary>
        private MaterialPropertyBlock BlockFor(Renderer renderer)
        {
            Material material = renderer.sharedMaterial;
            Color color = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
            _block.Clear();
            if (material != null && material.HasProperty(BaseMapId) && material.GetTexture(BaseMapId) != null)
            {
                _block.SetColor(BaseColorId, new Color(color.r * (1f - Mix), color.g * (1f - Mix), color.b * (1f - Mix), color.a));
                _block.SetColor(EmissionColorId, new Color(Grey * Mix, Grey * Mix, Grey * Mix, 1f));
            }
            else
            {
                float luminance = 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
                _block.SetColor(BaseColorId, new Color(Mathf.Lerp(color.r, luminance, Mix), Mathf.Lerp(color.g, luminance, Mix), Mathf.Lerp(color.b, luminance, Mix), color.a));
            }

            return _block;
        }
    }
}
