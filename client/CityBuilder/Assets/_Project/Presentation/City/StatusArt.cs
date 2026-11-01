#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CityBuilder.Client.Presentation.City
{
    /// <summary>
    /// The pictures every building's status shares (M0-50), made once for a <see cref="CityView"/> and destroyed with it: one ring mesh for each
    /// fill step (so showing a different progress swaps a reference and allocates nothing), the materials of the ring and the pips, a plain and
    /// an outlined disc, a one-pixel square, and one sprite for each icon texture that a bubble has shown. Everything is drawn with the render
    /// pipeline's default sprite material, so no shader is looked up by name (a lookup could be stripped from a device build). Display only.
    /// </summary>
    public sealed class StatusArt
    {
        /// <summary>How many steps the ring is drawn in; a progress is shown to the nearest lower step.</summary>
        public const int RingSteps = 32;

        /// <summary>The outer radius of the ring, in world units (a tile is 1 wide).</summary>
        public const float RingOuterRadius = 0.30f;

        /// <summary>The inner radius of the ring.</summary>
        public const float RingInnerRadius = 0.22f;

        /// <summary>The side of the square that a bubble's icon is drawn in.</summary>
        public const float IconSize = 0.30f;

        /// <summary>The size of the textures made for the discs, in pixels.</summary>
        private const int DiscPixels = 64;

        /// <summary>The colour of the part of the ring that is not done yet.</summary>
        public static readonly Color TrackColor = new Color(0.05f, 0.05f, 0.07f, 0.55f);

        /// <summary>The colour of the ring while a building is built.</summary>
        public static readonly Color ConstructingColor = new Color(0.96f, 0.76f, 0.26f);

        /// <summary>The colour of the ring while a building is upgraded.</summary>
        public static readonly Color UpgradingColor = new Color(0.36f, 0.66f, 0.96f);

        /// <summary>The colour of the ring while a building produces.</summary>
        public static readonly Color ProducingColor = new Color(0.46f, 0.82f, 0.36f);

        /// <summary>The light inside a bubble, behind its icon.</summary>
        public static readonly Color BubbleFaceColor = new Color(0.98f, 0.95f, 0.86f);

        /// <summary>The colour of a worker pip.</summary>
        public static readonly Color PipColor = new Color(0.98f, 0.93f, 0.62f);

        /// <summary>The material the sprites are drawn with: the pipeline's default one, shared, never destroyed here.</summary>
        private readonly Material _spriteMaterial;

        /// <summary>The ring meshes, one for each fill step from 0 (nothing) to <see cref="RingSteps"/> (the whole ring).</summary>
        private readonly Mesh[] _ringMeshes = new Mesh[RingSteps + 1];

        /// <summary>The materials made here, destroyed with the art.</summary>
        private readonly List<Material> _materials = new List<Material>();

        /// <summary>The textures made here, destroyed with the art.</summary>
        private readonly List<Texture2D> _textures = new List<Texture2D>();

        /// <summary>The sprites made here, destroyed with the art.</summary>
        private readonly List<Sprite> _sprites = new List<Sprite>();

        /// <summary>The sprite of each icon texture shown so far.</summary>
        private readonly Dictionary<Texture2D, Sprite> _iconSprites = new Dictionary<Texture2D, Sprite>();

        /// <summary>The fill materials by ring kind, in the order of <see cref="StatusRingKind"/> (the first is unused).</summary>
        private readonly Material[] _fillMaterials = new Material[4];

        /// <summary>Whether <see cref="Destroy"/> has run.</summary>
        private bool _destroyed;

        /// <summary>Makes the shared pictures. Needs Unity (textures, meshes and materials), so it is made when the first status is shown.</summary>
        public StatusArt()
        {
            _spriteMaterial = DefaultSpriteMaterial();
            TrackMaterial = Tint(TrackColor);
            _fillMaterials[(int)StatusRingKind.Constructing] = Tint(ConstructingColor);
            _fillMaterials[(int)StatusRingKind.Upgrading] = Tint(UpgradingColor);
            _fillMaterials[(int)StatusRingKind.Producing] = Tint(ProducingColor);
            MakeRingMeshes();
            Disc = MakeDisc(false);
            OutlinedDisc = MakeDisc(true);
            Square = MakeSquare();
        }

        /// <summary>The material of the ring's track (the part not done yet).</summary>
        public Material TrackMaterial { get; }

        /// <summary>A plain white disc of 1 unit across, to be tinted by the sprite renderer.</summary>
        public Sprite Disc { get; }

        /// <summary>A white disc of 1 unit across with a dark edge, for the pips.</summary>
        public Sprite OutlinedDisc { get; }

        /// <summary>A white square of 1 unit, to be stretched and tinted (the strike across a missing input).</summary>
        public Sprite Square { get; }

        /// <summary>The step a progress is drawn at: its permille scaled to <see cref="RingSteps"/>, rounded down, so only a finished job shows a full ring.</summary>
        /// <param name="progressPermille">The progress, 0 to 1000; values outside are brought into it.</param>
        /// <returns>0 to <see cref="RingSteps"/>.</returns>
        public static int StepOf(int progressPermille)
        {
            return Math.Max(0, Math.Min(RingSteps, (int)((long)Math.Max(0, progressPermille) * RingSteps / 1000)));
        }

        /// <summary>The mesh of a ring filled to <paramref name="step"/> steps, clockwise from the top.</summary>
        /// <param name="step">0 (empty) to <see cref="RingSteps"/> (whole).</param>
        public Mesh RingMesh(int step) => _ringMeshes[step];

        /// <summary>The material the filled part of a ring of <paramref name="kind"/> is drawn with.</summary>
        /// <param name="kind">A ring kind other than <see cref="StatusRingKind.None"/>.</param>
        public Material FillMaterial(StatusRingKind kind) => _fillMaterials[(int)kind];

        /// <summary>The colour of the rim of a bubble: amber for no workers, red for a missing input, blue for full storage.</summary>
        /// <param name="kind">The bubble.</param>
        public static Color BubbleColor(StatusBubbleKind kind)
        {
            switch (kind)
            {
                case StatusBubbleKind.NoWorkers:
                    return new Color(0.96f, 0.62f, 0.14f);
                case StatusBubbleKind.MissingInput:
                    return new Color(0.86f, 0.24f, 0.20f);
                case StatusBubbleKind.StorageFull:
                    return new Color(0.26f, 0.52f, 0.88f);
                default:
                    return Color.clear;
            }
        }

        /// <summary>The sprite of an icon texture, made the first time and kept after that, sized to <see cref="IconSize"/> in the world.</summary>
        /// <param name="icon">The picture.</param>
        public Sprite IconSprite(Texture2D icon)
        {
            if (!_iconSprites.TryGetValue(icon, out Sprite? sprite))
            {
                sprite = Sprite.Create(icon, new Rect(0f, 0f, icon.width, icon.height), new Vector2(0.5f, 0.5f), Math.Max(icon.width, icon.height) / IconSize, 0, SpriteMeshType.FullRect);
                _sprites.Add(sprite);
                _iconSprites.Add(icon, sprite);
            }

            return sprite;
        }

        /// <summary>A new renderer material of the default sprite look in <paramref name="color"/>; for the one a mesh ring is drawn with. Owned by the art.</summary>
        public Material Tint(Color color)
        {
            var material = new Material(_spriteMaterial) { color = color };
            _materials.Add(material);
            return material;
        }

        /// <summary>The material a sprite renderer starts with, shared by all the status sprites.</summary>
        public Material SpriteMaterial => _spriteMaterial;

        /// <summary>Destroys every mesh, material, texture and sprite made here. Safe to call twice.</summary>
        public void Destroy()
        {
            if (_destroyed)
            {
                return;
            }

            _destroyed = true;
            foreach (Mesh mesh in _ringMeshes)
            {
                PlaceholderArt.Destroy(mesh);
            }

            foreach (Material material in _materials)
            {
                PlaceholderArt.Destroy(material);
            }

            foreach (Sprite sprite in _sprites)
            {
                PlaceholderArt.Destroy(sprite);
            }

            foreach (Texture2D texture in _textures)
            {
                PlaceholderArt.Destroy(texture);
            }

            _materials.Clear();
            _sprites.Clear();
            _textures.Clear();
            _iconSprites.Clear();
        }

        /// <summary>The material a new sprite renderer gets from the render pipeline, found by making one and reading it (no shader name is looked up).</summary>
        private static Material DefaultSpriteMaterial()
        {
            var probe = new GameObject("Sprite material probe");
            try
            {
                return probe.AddComponent<SpriteRenderer>().sharedMaterial;
            }
            finally
            {
                PlaceholderArt.Destroy(probe);
            }
        }

        /// <summary>Makes the <see cref="RingSteps"/> + 1 ring meshes: the same vertices, and the triangles of the first <c>step</c> segments.</summary>
        private void MakeRingMeshes()
        {
            var vertices = new Vector3[2 * (RingSteps + 1)];
            var uvs = new Vector2[vertices.Length];
            for (int k = 0; k <= RingSteps; k++)
            {
                // Clockwise from the top: the angle starts at 90 degrees and falls.
                float angle = Mathf.PI / 2f - 2f * Mathf.PI * k / RingSteps;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertices[2 * k] = direction * RingOuterRadius;
                vertices[2 * k + 1] = direction * RingInnerRadius;
                uvs[2 * k] = new Vector2(0.5f, 0.5f);
                uvs[2 * k + 1] = new Vector2(0.5f, 0.5f);
            }

            for (int step = 0; step <= RingSteps; step++)
            {
                var triangles = new int[6 * step];
                for (int k = 0; k < step; k++)
                {
                    int a = 2 * k;
                    int t = 6 * k;
                    triangles[t] = a;
                    triangles[t + 1] = a + 2;
                    triangles[t + 2] = a + 1;
                    triangles[t + 3] = a + 1;
                    triangles[t + 4] = a + 2;
                    triangles[t + 5] = a + 3;
                }

                var mesh = new Mesh { name = "Status ring " + step, vertices = vertices, uv = uvs, triangles = triangles };
                mesh.RecalculateBounds();
                _ringMeshes[step] = mesh;
            }
        }

        /// <summary>Makes a white disc that fills its square, 1 unit across; with <paramref name="outlined"/> its edge is dark.</summary>
        private Sprite MakeDisc(bool outlined)
        {
            var pixels = new Color32[DiscPixels * DiscPixels];
            float center = (DiscPixels - 1) / 2f;
            for (int y = 0; y < DiscPixels; y++)
            {
                for (int x = 0; x < DiscPixels; x++)
                {
                    float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / (DiscPixels / 2f);
                    float alpha = Mathf.Clamp01((1f - distance) * 12f);
                    byte shade = outlined && distance > 0.68f ? (byte)30 : (byte)255;
                    pixels[y * DiscPixels + x] = new Color32(shade, shade, shade, (byte)(alpha * 255f));
                }
            }

            return MakeSprite(pixels, DiscPixels);
        }

        /// <summary>Makes a white 4 by 4 square that is 1 unit across.</summary>
        private Sprite MakeSquare()
        {
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            return MakeSprite(pixels, 4);
        }

        /// <summary>Wraps square <paramref name="pixels"/> of <paramref name="size"/> by <paramref name="size"/> in a texture and a sprite 1 unit across.</summary>
        private Sprite MakeSprite(Color32[] pixels, int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            _textures.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            _sprites.Add(sprite);
            return sprite;
        }
    }
}
