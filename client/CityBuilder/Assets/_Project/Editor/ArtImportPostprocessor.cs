#nullable enable

using UnityEditor;
using UnityEngine;

namespace CityBuilder.Client.Editor
{
    /// <summary>
    /// Applies the art's import settings, so nobody sets them by hand (<c>09</c> rule 1, CR-0005): the palette texture keeps its hard-edged
    /// swatches, every model under <c>Art/Resources/Models</c> comes in at scale 1, without cameras or lights, drawn with the one shared material,
    /// and every UI icon under <c>Resources/UI/Icons</c> comes in smooth, without mips, as a picture with a transparent background.
    /// </summary>
    internal sealed class ArtImportPostprocessor : AssetPostprocessor
    {
        /// <summary>The folder of the FBX files the Blender build writes (under <c>Resources</c>, so a player build contains them and the client loads them by name) (<c>art/README.md</c>).</summary>
        private const string _modelsFolder = "Assets/_Project/Art/Resources/Models/";
        /// <summary>The folder of the palette textures, one per era.</summary>
        private const string _texturesFolder = "Assets/_Project/Art/Textures/";
        /// <summary>The folder of the UI icons (PNG, 256 px) the client loads by name (<c>UiIcons</c>).</summary>
        private const string _iconsFolder = "Assets/_Project/Resources/UI/Icons/";
        /// <summary>The material name every FBX of Era I carries (<c>MATERIAL_NAME</c> in <c>art/blender/lib.py</c>); this is what gets remapped.</summary>
        private const string _sourceMaterialName = "M_Palette_Era1";
        /// <summary>The Era I palette; the shared material's Base Map.</summary>
        private const string _palettePath = "Assets/_Project/Art/Textures/palette_era1.png";
        /// <summary>The one material every model uses; made by <see cref="EnsureSharedMaterial"/>.</summary>
        private const string _sharedMaterialPath = "Assets/_Project/Art/Materials/M_Palette_Era1.mat";
        /// <summary>The folder of <see cref="_sharedMaterialPath"/>.</summary>
        private const string _materialsFolder = "Assets/_Project/Art/Materials";
        /// <summary>The URP shader of the shared material.</summary>
        private const string _litShaderName = "Universal Render Pipeline/Lit";
        /// <summary>The emission of the shared material: darker than one step of an 8-bit channel, so it shows nothing but keeps <c>_EMISSION</c> on.</summary>
        private const float _emissionBias = 0.002f;
        /// <summary>Smoothness of the shared material: nearly matte, like painted toy blocks (<c>art/README.md</c>).</summary>
        private const float _smoothness = 0.1f;

        /// <summary>
        /// Gives a texture its settings before Unity imports it. A palette: no filtering, compression or mips, which would blend neighbouring
        /// swatches. A UI icon: bilinear, no mips (it is drawn at a fixed size), transparent background without a halo at the edge.
        /// </summary>
        private void OnPreprocessTexture()
        {
            var importer = (TextureImporter)assetImporter;
            if (assetPath.StartsWith(_iconsFolder, System.StringComparison.Ordinal))
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.isReadable = false;
                return;
            }

            if (!assetPath.StartsWith(_texturesFolder, System.StringComparison.Ordinal))
            {
                return;
            }

            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
        }

        /// <summary>Gives a model its settings before Unity imports it. The material link needs the material asset, so <see cref="OnPostprocessAllAssets"/> adds it.</summary>
        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(_modelsFolder, System.StringComparison.Ordinal))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.addCollider = false;
            importer.isReadable = false;
            // The flat shading comes from the file's face normals; URP Lit without a normal map needs no tangents.
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        }

        /// <summary>
        /// Links each newly imported model to the shared material, once Unity has finished importing: loading another asset is only safe
        /// here, not in a preprocess step. The link is kept in the model's <c>.meta</c> file, so a model that has it is left alone.
        /// </summary>
        /// <param name="importedAssets">The paths Unity has just imported.</param>
        /// <param name="deletedAssets">Not used; Unity's signature.</param>
        /// <param name="movedAssets">Not used; Unity's signature.</param>
        /// <param name="movedFromAssetPaths">Not used; Unity's signature.</param>
        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            var source = new AssetImporter.SourceAssetIdentifier(typeof(Material), _sourceMaterialName);
            foreach (string path in importedAssets)
            {
                if (!path.StartsWith(_modelsFolder, System.StringComparison.Ordinal)
                    || !(AssetImporter.GetAtPath(path) is ModelImporter importer)
                    || importer.GetExternalObjectMap().ContainsKey(source))
                {
                    continue;
                }

                Material? material = EnsureSharedMaterial();
                if (material == null)
                {
                    return;
                }

                importer.AddRemap(source, material);
                importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// The shared material, made from the palette when it does not exist yet. Null, after one logged error that names what is missing, when
        /// the palette or the shader cannot be found.
        /// </summary>
        /// <returns>The material asset, or null.</returns>
        private static Material? EnsureSharedMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(_sharedMaterialPath);
            if (existing != null)
            {
                return existing;
            }

            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(_palettePath);
            Shader shader = Shader.Find(_litShaderName);
            if (palette == null || shader == null)
            {
                Debug.LogError($"Cannot make {_sharedMaterialPath}: need the texture {_palettePath} and the shader {_litShaderName}.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(_materialsFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Art", "Materials");
            }

            var material = new Material(shader) { name = _sourceMaterialName };
            material.SetTexture("_BaseMap", palette);
            material.SetFloat("_Smoothness", _smoothness);

            // A stalled building's property block adds a grey as emission (StalledTint), which needs the keyword on. URP's material code switches
            // the keyword off while the material is flagged as having black emission, so it gets an emission too dark to see (half a level of 255)
            // and the baked-emission flag that the Editor's own emission toggle sets.
            material.SetColor("_EmissionColor", new Color(_emissionBias, _emissionBias, _emissionBias, 1f));
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            material.EnableKeyword("_EMISSION");
            AssetDatabase.CreateAsset(material, _sharedMaterialPath);
            return material;
        }
    }
}
