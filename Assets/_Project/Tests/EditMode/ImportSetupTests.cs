using System.IO;
using Kendo.Characters;
using Kendo.Core;
using Kendo.EditorTools.Import;
using Kendo.EditorTools.Setup;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Kendo.Tests
{
    public sealed class ImportSetupTests
    {
        [Test]
        public void EveryModelUnderArtHasAnImportRule()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { KendoImportRules.ArtRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                Assert.IsTrue(KendoImportRules.TryGet(path, out _), $"No import rule for {path}");
            }
        }

        [Test]
        public void EveryImportRulePointsAtAnExistingFile()
        {
            foreach (var rule in KendoImportRules.All)
                Assert.IsTrue(File.Exists(rule.AssetPath), $"Missing {rule.AssetPath}");
        }

        [Test]
        public void ImportersMatchTheirRules()
        {
            foreach (var rule in KendoImportRules.All)
            {
                var importer = AssetImporter.GetAtPath(rule.AssetPath) as ModelImporter;
                Assert.IsNotNull(importer, rule.AssetPath);
                Assert.AreEqual(rule.GlobalScale, importer.globalScale, 1e-4f, $"{rule.FileName} scale");
                Assert.IsFalse(importer.importLights, $"{rule.FileName} imports lights");
                Assert.IsFalse(importer.importCameras, $"{rule.FileName} imports cameras");
            }
        }

        [Test]
        public void KendoMasterPrefabIsLifeSize()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KendoPrefabBuilder.MasterPrefab);
            if (prefab == null) Assert.Ignore("Run Kendo > Setup > Build Prefabs first.");
            var model = prefab.GetComponent<KendoCharacterModel>();
            Assert.IsNotNull(model);
            Assert.That(model.MeasuredHeight, Is.InRange(AssetCalibration.MasterHeightRange.x, AssetCalibration.MasterHeightRange.y));
            Assert.IsNotNull(model.ShinaiRestSocket.GetComponentInChildren<ShinaiModel>(), "Master should carry the supplied shinai");
        }

        [Test]
        public void ShinaiPrefabHasRealisticLengthAndFrame()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KendoPrefabBuilder.ShinaiPrefab);
            if (prefab == null) Assert.Ignore("Run Kendo > Setup > Build Prefabs first.");
            var shinai = prefab.GetComponent<ShinaiModel>();
            Assert.IsNotNull(shinai);
            Assert.That(shinai.Length, Is.InRange(AssetCalibration.ShinaiLengthRange.x, AssetCalibration.ShinaiLengthRange.y));
            Assert.Greater(Vector3.Dot(shinai.Tip.localPosition.normalized, Vector3.up), 0.99f, "Shinai should point along +Y");
        }

        [Test]
        public void BadgePrefabsAreHandSized()
        {
            for (var i = 1; i <= 4; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KendoPrefabBuilder.BadgePrefab(i));
                if (prefab == null) Assert.Ignore("Run Kendo > Setup > Build Prefabs first.");
                var instance = Object.Instantiate(prefab);
                try
                {
                    var size = KendoPrefabBuilder.RendererBounds(instance.transform).size;
                    var diameter = Mathf.Max(size.x, size.z);
                    Assert.That(diameter, Is.InRange(AssetCalibration.BadgeDiameterRange.x, AssetCalibration.BadgeDiameterRange.y), $"Badge_0{i}");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }
    }
}
