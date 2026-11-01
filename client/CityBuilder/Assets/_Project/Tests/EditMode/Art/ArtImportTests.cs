#nullable enable

using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CityBuilder.Client.Tests.EditMode.Art
{
    /// <summary>
    /// The art's import (CR-0005-T2): what <c>ArtImportPostprocessor</c> gives the palette and the models, that all of them draw with the one
    /// shared material, and that <c>house_l1</c>, the first building, arrives at the size and facing <c>art/README.md</c> promises.
    /// </summary>
    public sealed class ArtImportTests
    {
        /// <summary>The folder of the FBX files.</summary>
        private const string _modelsFolder = "Assets/_Project/Art/Models";
        /// <summary>The Era I palette.</summary>
        private const string _palettePath = "Assets/_Project/Art/Textures/palette_era1.png";
        /// <summary>The one material of the models.</summary>
        private const string _materialPath = "Assets/_Project/Art/Materials/M_Palette_Era1.mat";
        /// <summary>The first building.</summary>
        private const string _housePath = "Assets/_Project/Art/Models/era1/house_l1.fbx";
        /// <summary>The folder <see cref="NewModel_OnImport_GetsTheSettingsAndTheSharedMaterial"/> makes and removes.</summary>
        private const string _temporaryFolder = "Assets/_Project/Art/Models/ArtImportTestsTemporary";
        /// <summary>How far a building may reach from its pivot on the ground (<c>art/README.md</c> → Scale and orientation).</summary>
        private const float _tileReach = 0.475f;
        /// <summary>Room for rounding in the import, in Unity units.</summary>
        private const float _tolerance = 0.001f;

        /// <summary>After each test: removes what the new-model test made, also when it failed.</summary>
        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(_temporaryFolder);
        }

        /// <summary>The palette has hard swatches: no filtering, no mips, no compression, no wrap into the neighbouring edge.</summary>
        [Test]
        public void Palette_ImportSettings_KeepTheSwatchesHard()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(_palettePath);

            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
        }

        /// <summary>The shared material is URP Lit, its Base Map is the palette, and it is nearly matte.</summary>
        [Test]
        public void SharedMaterial_Asset_IsUrpLitOverThePalette()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(_materialPath);

            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(material.GetTexture("_BaseMap"), Is.EqualTo(AssetDatabase.LoadAssetAtPath<Texture2D>(_palettePath)));
            Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(0.1f).Within(_tolerance));
        }

        /// <summary>Every model has the settings of the postprocessor.</summary>
        [Test]
        public void EveryModel_ImportSettings_AreThePostprocessors()
        {
            foreach (string path in ModelPaths())
            {
                AssertHasModelSettings(path);
            }
        }

        /// <summary>Every model draws with the shared material only, which is what keeps a whole city at a few draw calls.</summary>
        [Test]
        public void EveryModel_Renderers_UseOnlyTheSharedMaterial()
        {
            foreach (string path in ModelPaths())
            {
                AssertUsesOnlyTheSharedMaterial(path);
            }
        }

        /// <summary>The scale and the axes are baked into the vertices: every model's root sits untransformed, with no camera or light inside.</summary>
        [Test]
        public void EveryModel_Root_IsUntransformedAndHoldsNoCameraOrLight()
        {
            foreach (string path in ModelPaths())
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                Assert.That(model.transform.localScale, Is.EqualTo(Vector3.one), path);
                Assert.That(model.transform.localRotation, Is.EqualTo(Quaternion.identity), path);
                Assert.That(model.GetComponentsInChildren<Camera>(true), Is.Empty, path);
                Assert.That(model.GetComponentsInChildren<Light>(true), Is.Empty, path);
            }
        }

        /// <summary>A building's size arrives unchanged: within its tile, standing on the ground, and clearly more than a sliver of it.</summary>
        [Test]
        public void House_Bounds_FillItsTileAndStandOnTheGround()
        {
            var house = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(_housePath));
            try
            {
                Bounds bounds = BoundsOf(house);

                Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(-_tileReach - _tolerance));
                Assert.That(bounds.max.x, Is.LessThanOrEqualTo(_tileReach + _tolerance));
                Assert.That(bounds.min.z, Is.GreaterThanOrEqualTo(-_tileReach - _tolerance));
                Assert.That(bounds.max.z, Is.LessThanOrEqualTo(_tileReach + _tolerance));
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(_tolerance));
                Assert.That(bounds.size.x, Is.GreaterThan(_tileReach), "a wrong file scale would shrink or grow the model");
            }
            finally
            {
                Object.DestroyImmediate(house);
            }
        }

        /// <summary>The anchors the views will look for exist, the door faces −Z and the status anchor sits at the top of the model.</summary>
        [Test]
        public void House_Anchors_AreWhereTheArtSaysTheyAre()
        {
            var house = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(_housePath));
            try
            {
                Transform door = house.transform.Find("Anchor_Door");
                Transform status = house.transform.Find("Anchor_Status");

                Assert.That(door, Is.Not.Null);
                Assert.That(status, Is.Not.Null);
                Assert.That(door.position.z, Is.LessThan(0f));
                Assert.That(status.position.y, Is.GreaterThanOrEqualTo(BoundsOf(house).max.y - _tolerance));
            }
            finally
            {
                Object.DestroyImmediate(house);
            }
        }

        /// <summary>A model the Blender build adds later has no <c>.meta</c> file: its first import alone gives it the settings and the material.</summary>
        [Test]
        public void NewModel_OnImport_GetsTheSettingsAndTheSharedMaterial()
        {
            AssetDatabase.CreateFolder(_modelsFolder, Path.GetFileName(_temporaryFolder));
            string path = _temporaryFolder + "/new_model.fbx";
            File.Copy(_housePath, path);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            AssertHasModelSettings(path);
            AssertUsesOnlyTheSharedMaterial(path);
        }

        /// <summary>Asserts the import settings of one model.</summary>
        /// <param name="path">The model's asset path.</param>
        private static void AssertHasModelSettings(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);

            Assert.That(importer.globalScale, Is.EqualTo(1f), path);
            Assert.That(importer.importCameras, Is.False, path);
            Assert.That(importer.importLights, Is.False, path);
            Assert.That(importer.importAnimation, Is.False, path);
            Assert.That(importer.addCollider, Is.False, path);
        }

        /// <summary>Asserts that each renderer of one model has one material, the shared asset.</summary>
        /// <param name="path">The model's asset path.</param>
        private static void AssertUsesOnlyTheSharedMaterial(string path)
        {
            var shared = AssetDatabase.LoadAssetAtPath<Material>(_materialPath);
            Renderer[] renderers = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<Renderer>(true);

            Assert.That(renderers, Is.Not.Empty, path);
            foreach (Renderer renderer in renderers)
            {
                Assert.That(renderer.sharedMaterials, Is.EqualTo(new[] { shared }), path);
            }
        }

        /// <summary>The paths of all the models; fails when there are none, so the tests over them cannot pass empty.</summary>
        /// <returns>The asset paths of the FBX files.</returns>
        private static string[] ModelPaths()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { _modelsFolder });
            Assert.That(guids, Is.Not.Empty);

            var paths = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
            }

            return paths;
        }

        /// <summary>The world bounds of all the renderers of an instance.</summary>
        /// <param name="instance">The object in the scene.</param>
        /// <returns>The bounds around its renderers.</returns>
        private static Bounds BoundsOf(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            return bounds;
        }
    }
}
