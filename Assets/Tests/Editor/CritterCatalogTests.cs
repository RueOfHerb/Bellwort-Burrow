using System.Linq;
using NUnit.Framework;
using UnityEditor;
using BellwortBurrow.Data;

namespace BellwortBurrow.Tests.Editor
{
    public class CritterCatalogTests
    {
        const string CatalogPath = "Assets/Data/Characters/CritterCatalog.asset";
        const string PlayerFolder = "Assets/Art/Characters/Player";

        [Test]
        public void EveryPlayerFile_HasACritterKindInTheCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CritterCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, $"{CatalogPath} is missing.");

            if (!AssetDatabase.IsValidFolder(PlayerFolder)) Assert.Ignore("No player files here. Install the art package to check them.");
            var files = AssetDatabase.FindAssets("", new[] { PlayerFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".aseprite"))
                .ToList();
            if (files.Count == 0) Assert.Ignore("No player files here. Install the art package to check them.");

            foreach (var file in files)
            {
                bool listed = catalog.Kinds.Any(kind => kind != null && kind.Sprite != null && AssetDatabase.GetAssetPath(kind.Sprite) == file);
                Assert.IsTrue(listed, $"{file} has no Critter Kind in the Critter Catalog, so the character creator won't offer it.");
            }
        }

        [Test]
        public void CritterKinds_HaveUniqueIds()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CritterCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, $"{CatalogPath} is missing.");

            var ids = catalog.Kinds.Where(kind => kind != null).Select(kind => kind.Id).ToList();
            Assert.IsTrue(ids.All(id => !string.IsNullOrEmpty(id)), "Every Critter Kind needs an id.");
            Assert.AreEqual(ids.Count, ids.Distinct().Count(), "Two Critter Kinds share an id.");
            Assert.AreEqual(catalog.Kinds.Count, ids.Count, "The Critter Catalog has an empty slot.");
        }
    }
}
