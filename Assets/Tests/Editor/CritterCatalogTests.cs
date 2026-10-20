using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using BellwortBurrow.Data;
using BellwortBurrow.EditorTools;

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

        /// <summary>
        /// Every coat sits on the same steps of its fur ramp: Base is the main color and Shadow the outline, so a color
        /// picked in the creator looks the same on every critter (Jocelyn, Oct 19).
        /// </summary>
        [Test]
        public void EveryCoat_SitsOnTheSameSteps()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CritterCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, $"{CatalogPath} is missing.");
            if (catalog.Palette == null) Assert.Ignore("No characters palette here. Install the art package to check it.");

            int checkedKinds = 0;
            foreach (var kind in catalog.Kinds.Where(kind => kind != null && kind.Sprite != null))
            {
                string file = AssetDatabase.GetAssetPath(kind.Sprite);
                var fur = catalog.Palette.Find(kind.FurRamp);
                if (!file.EndsWith(".aseprite") || fur == null) continue;
                var sheet = AsepriteSheet.Read(File.ReadAllBytes(file));

                var inside = new int[fur.colors.Length];
                var edge = new int[fur.colors.Length];
                for (int y = 0; y < sheet.Height; y++)
                for (int x = 0; x < sheet.Width; x++)
                {
                    if (!Opaque(sheet, x, y)) continue;
                    int i = (y * sheet.Width + x) * 4;
                    int step = System.Array.IndexOf(fur.colors, new Color32(sheet.Art[i], sheet.Art[i + 1], sheet.Art[i + 2], 255));
                    if (step < 0) continue;
                    bool onEdge = !Opaque(sheet, x - 1, y) || !Opaque(sheet, x + 1, y) || !Opaque(sheet, x, y - 1) || !Opaque(sheet, x, y + 1);
                    if (onEdge) edge[step]++; else inside[step]++;
                }
                Assert.AreEqual(2, MostUsed(inside), $"{kind.Id}'s coat should be mostly {fur.name} Base inside its outline, the main color of every coat.");
                Assert.AreEqual(0, MostUsed(edge), $"{kind.Id}'s outline should be {fur.name} Shadow, like every coat.");
                checkedKinds++;
            }
            if (checkedKinds == 0) Assert.Ignore("No player files here. Install the art package to check them.");
        }

        static bool Opaque(AsepriteSheet sheet, int x, int y) =>
            x >= 0 && y >= 0 && x < sheet.Width && y < sheet.Height && sheet.Art[(y * sheet.Width + x) * 4 + 3] > 0;

        static int MostUsed(int[] counts)
        {
            int best = 0;
            for (int i = 1; i < counts.Length; i++) if (counts[i] > counts[best]) best = i;
            return best;
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

            foreach (var part in new[] { AppearancePart.FurColor, AppearancePart.MarkingsColor, AppearancePart.EyeColor, AppearancePart.TopColor, AppearancePart.BottomColor })
            {
                var choices = options.For(part);
                Assert.AreEqual(RampSwap.Natural, choices[0].id, $"{part} should start on Natural.");
                foreach (var choice in choices.Skip(1))
                    Assert.IsNotNull(catalog.Palette.Find(choice.id), $"{part} offers '{choice.id}', which isn't a ramp in the characters palette.");
            }
        }

        [Test]
        public void EveryFace_HasEyesAndAMouth_StartingWithDefault()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CritterCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, $"{CatalogPath} is missing.");
            if (catalog.Faces.Count == 0) Assert.Ignore("No faces here. Install the art package to check them.");

            Assert.AreEqual("default", catalog.Faces[0]?.id, "Every critter starts on the Default face, so it comes first.");
            var ids = catalog.Faces.Where(face => face != null).Select(face => face.id).ToList();
            Assert.AreEqual(catalog.Faces.Count, ids.Count, "The face list has an empty slot.");
            Assert.AreEqual(ids.Count, ids.Distinct().Count(), "Two faces share an id.");
            foreach (var face in catalog.Faces)
            {
                Assert.IsFalse(string.IsNullOrEmpty(face.id), "Every face needs an id.");
                Assert.IsNotNull(face.eyes, $"Face {face.id} has no eyes sprite.");
                Assert.IsNotNull(face.mouth, $"Face {face.id} has no mouth sprite.");
                Assert.AreEqual(face.eyes.name + "_mouth", face.mouth.name, $"Face {face.id}'s mouth should be its own face_<name>_mouth slice.");
            }
        }

        [Test]
        public void TheWardrobe_StartsInTodaysClothes_AndEveryPieceIsDrawn()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CritterCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, $"{CatalogPath} is missing.");
            var wardrobe = catalog.Wardrobe;
            Assert.IsNotNull(wardrobe, "The Critter Catalog has no Wardrobe.");
            if (wardrobe.Pieces.All(piece => piece == null || piece.sprite == null)) Assert.Ignore("No clothes here. Install the art package to check them.");

            var firsts = new Dictionary<ClothingSlot, string>
            {
                [ClothingSlot.Top] = "tunic", [ClothingSlot.Bottom] = "trousers", [ClothingSlot.Waist] = "belt",
                [ClothingSlot.Neck] = Wardrobe.NoneId, [ClothingSlot.Ears] = Wardrobe.NoneId,
            };
            foreach (var pair in firsts)
                Assert.AreEqual(pair.Value, wardrobe.For(pair.Key).FirstOrDefault()?.id, $"A new character wears {pair.Value} in {pair.Key}, so it comes first.");

            foreach (ClothingSlot slot in System.Enum.GetValues(typeof(ClothingSlot)))
            {
                var pieces = wardrobe.For(slot);
                Assert.AreEqual(pieces.Count, pieces.Select(piece => piece.id).Distinct().Count(), $"Two {slot} pieces share an id.");
                foreach (var piece in pieces)
                {
                    if (piece.id == Wardrobe.NoneId) { Assert.IsNull(piece.sprite, "None draws nothing."); continue; }
                    Assert.IsNotNull(piece.sprite, $"{slot} {piece.id} has no sprite.");
                    Assert.IsTrue(piece.sprite.name.StartsWith(slot.ToString().ToLowerInvariant() + "_"), $"{slot} {piece.id} shows {piece.sprite.name}, a slice from another row.");
                    if (slot is ClothingSlot.Top or ClothingSlot.Bottom)
                        Assert.IsNotNull(catalog.Palette != null ? catalog.Palette.Find(piece.ramp) : null, $"{slot} {piece.id} has to name the ramp it's drawn in, so its color row can swap it.");
                    else
                        Assert.IsTrue(string.IsNullOrEmpty(piece.ramp), $"{slot} {piece.id}: accessories keep their colors.");
                }
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
