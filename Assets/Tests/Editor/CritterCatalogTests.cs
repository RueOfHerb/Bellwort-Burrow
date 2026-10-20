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
        public void EveryKind_IsDrawnInRampsThePaletteHas()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CritterCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, $"{CatalogPath} is missing.");
            if (catalog.Palette == null) Assert.Ignore("No characters palette here. Install the art package to check it.");

            foreach (var kind in catalog.Kinds.Where(kind => kind != null))
            {
                Assert.IsNotNull(catalog.Palette.Find(kind.FurRamp), $"{kind.Id}'s fur ramp '{kind.FurRamp}' isn't in the characters palette.");
                Assert.IsNotNull(catalog.Palette.Find(kind.MarkingsRamp), $"{kind.Id}'s markings ramp '{kind.MarkingsRamp}' isn't in the characters palette.");
                Assert.IsNotNull(catalog.Palette.Find(kind.EyeRamp), $"{kind.Id}'s eye ramp '{kind.EyeRamp}' isn't in the characters palette.");
            }
        }

        [Test]
        public void ThePalettesColors_AreAllDifferent()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CritterCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, $"{CatalogPath} is missing.");
            if (catalog.Palette == null) Assert.Ignore("No characters palette here. Install the art package to check it.");

            var seen = new System.Collections.Generic.Dictionary<UnityEngine.Color32, string>();
            foreach (var ramp in catalog.Palette.Ramps)
            {
                foreach (var color in ramp.colors)
                {
                    Assert.IsFalse(seen.TryGetValue(color, out var other), $"{ramp.name} and {other} share a color, so recoloring can't tell them apart.");
                    seen[color] = ramp.name;
                }
            }
        }

        [Test]
        public void EveryColorChoice_IsNaturalOrARamp()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CritterCatalog>(CatalogPath);
            var options = AssetDatabase.LoadAssetAtPath<CharacterOptions>("Assets/Data/Characters/CharacterOptions.asset");
            Assert.IsNotNull(options, "CharacterOptions.asset is missing.");
            if (catalog == null || catalog.Palette == null) Assert.Ignore("No characters palette here. Install the art package to check it.");

            foreach (var part in new[] { AppearancePart.FurColor, AppearancePart.MarkingsColor, AppearancePart.EyeColor })
            {
                var choices = options.For(part);
                Assert.AreEqual(RampSwap.Natural, choices[0].id, $"{part} should start on Natural.");
                foreach (var choice in choices.Skip(1))
                    Assert.IsNotNull(catalog.Palette.Find(choice.id), $"{part} offers '{choice.id}', which isn't a ramp in the characters palette.");
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
