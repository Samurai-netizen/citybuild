#nullable enable

using UnityEditor;
using UnityEngine;

namespace CityBuilder.Client.Editor
{
    /// <summary>
    /// Applies the art's import settings, so nobody sets them by hand (<c>09</c> rule 1, CR-0005): the palette texture keeps its hard-edged
    /// swatches, and every model under <c>Art/Models</c> comes in at scale 1, without cameras or lights, drawn with the one shared material.
    /// </summary>
    internal sealed class ArtImportPostprocessor : AssetPostprocessor
    {
        /// <summary>The folder of the FBX files the Blender build writes (<c>art/README.md</c>).</summary>
        private const string _modelsFolder = "Assets/_Project/Art/Models/";
        /// <summary>The folder of the palette textures, one per era.</summary>
        private const string _texturesFolder = "Assets/_Project/Art/Textures/";
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
        /// <summary>Smoothness of the shared material: nearly matte, like painted toy blocks (<c>art/README.md</c>).</summary>
        private const float _smoothness = 0.1f;

        /// <summary>Gives a palette texture its settings before Unity imports it: no filtering, compression or mips, which would blend neighbouring swatches.</summary>
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(_texturesFolder, System.StringComparison.Ordinal))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
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
            AssetDatabase.CreateAsset(material, _sharedMaterialPath);
            return material;
        }
    }
}
