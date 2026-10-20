using System;
using NUnit.Framework;
using UnityEngine;
using BellwortBurrow.Data;
using BellwortBurrow.Systems.Profile;

namespace BellwortBurrow.Tests.EditMode
{
    public class PlayerProfileTests
    {
        static CritterCatalog MakeCatalog(params string[] ids)
        {
            var kinds = Array.ConvertAll(ids, id =>
            {
                var kind = ScriptableObject.CreateInstance<CritterKind>();
                kind.ConfigureForTesting(id, id + " name", null);
                return kind;
            });
            var catalog = ScriptableObject.CreateInstance<CritterCatalog>();
            catalog.ConfigureForTesting(kinds);
            return catalog;
        }

        static CharacterOptions MakeOptions()
        {
            var options = ScriptableObject.CreateInstance<CharacterOptions>();
            options.ConfigureForTesting(new[]
            {
                new AppearanceOptionList { part = AppearancePart.FurColor, options = { new AppearanceOption("orange", "Orange"), new AppearanceOption("white", "White") } },
                new AppearanceOptionList { part = AppearancePart.Face, options = { new AppearanceOption("default", "Default") } }
            });
            return options;
        }

        [Test]
        public void GetAndSet_CoverEveryPart()
        {
            var profile = new PlayerProfile();
            foreach (AppearancePart part in Enum.GetValues(typeof(AppearancePart)))
                profile.Set(part, "id-" + part);

            foreach (AppearancePart part in Enum.GetValues(typeof(AppearancePart)))
                Assert.AreEqual("id-" + part, profile.Get(part));
        }

        [Test]
        public void Clone_IsIndependentOfTheOriginal()
        {
            var profile = new PlayerProfile { name = "Bryn", critterKind = "fox" };
            var copy = profile.Clone();
            copy.critterKind = "mouse";

            Assert.AreEqual("fox", profile.critterKind);
            Assert.AreEqual("Bryn", copy.name);
        }

        [Test]
        public void Step_LoopsAroundAtBothEnds()
        {
            var choices = AppearanceChoices.For(AppearancePart.CritterKind, MakeCatalog("mouse", "rabbit", "fox"), null);

            Assert.AreEqual("rabbit", AppearanceChoices.Step(choices, "mouse", 1));
            Assert.AreEqual("mouse", AppearanceChoices.Step(choices, "fox", 1));
            Assert.AreEqual("fox", AppearanceChoices.Step(choices, "mouse", -1));
        }

        [Test]
        public void Step_FromAnUnknownId_CountsFromTheFirstChoice()
        {
            var choices = AppearanceChoices.For(AppearancePart.CritterKind, MakeCatalog("mouse", "rabbit"), null);

            Assert.AreEqual("rabbit", AppearanceChoices.Step(choices, "otter", 1));
        }

        [Test]
        public void Step_WithNoChoices_KeepsTheId()
        {
            var choices = AppearanceChoices.For(AppearancePart.Waist, null, MakeOptions());

            Assert.AreEqual("", AppearanceChoices.Step(choices, "", 1));
            Assert.AreEqual(AppearanceChoices.NoChoice, AppearanceChoices.DisplayName(choices, ""));
        }

        [Test]
        public void Defaults_PickTheFirstChoiceOfEveryPart()
        {
            var profile = AppearanceChoices.Defaults(MakeCatalog("mouse", "fox"), MakeOptions());

            Assert.AreEqual("mouse", profile.critterKind);
            Assert.AreEqual("orange", profile.furColor);
            Assert.AreEqual("default", profile.face);
            Assert.AreEqual("", profile.waist);
            Assert.AreEqual("", profile.name);
        }

        [Test]
        public void ClothingChoices_ComeFromTheWardrobe_ASlotAtATime()
        {
            var wardrobe = ScriptableObject.CreateInstance<Wardrobe>();
            wardrobe.ConfigureForTesting(new[]
            {
                new ClothingPiece("tunic", "Tunic", ClothingSlot.Top, null, "Blue"),
                new ClothingPiece("dress", "Dress", ClothingSlot.Top, null, "Purple", coversBottom: true),
                new ClothingPiece("trousers", "Trousers", ClothingSlot.Bottom, null, "Taupe"),
                new ClothingPiece("none", "None", ClothingSlot.Ears, null),
                new ClothingPiece("studs", "Studs", ClothingSlot.Ears, null, paired: true),
            }, new Vector2Int(5, 9));
            var catalog = MakeCatalog("mouse");
            catalog.ConfigureForTesting(catalog.Kinds, wardrobe: wardrobe);

            var tops = AppearanceChoices.For(AppearancePart.Top, catalog, MakeOptions());
            var defaults = AppearanceChoices.Defaults(catalog, MakeOptions());

            Assert.AreEqual(new[] { "tunic", "dress" }, new[] { tops[0].id, tops[1].id });
            Assert.AreEqual("Dress", tops[1].displayName);
            Assert.AreEqual("tunic", defaults.top, "a new character wears each slot's first piece");
            Assert.AreEqual("none", defaults.ears);
            Assert.AreEqual("studs", wardrobe.Find(ClothingSlot.Ears, "studs").id);
            Assert.AreEqual("tunic", wardrobe.Find(ClothingSlot.Top, "tie").id, "an id from another slot gets the slot's first piece");
        }

        [Test]
        public void OldSaves_KeepTheirShirtTrousersAndBeltAsTopBottomAndWaist()
        {
            var loaded = JsonUtility.FromJson<PlayerProfile>("{\"shirt\":\"tunic\",\"trousers\":\"trousers\",\"belt\":\"belt\"}");

            Assert.AreEqual("tunic", loaded.top);
            Assert.AreEqual("trousers", loaded.bottom);
            Assert.AreEqual("belt", loaded.waist);
        }

        [Test]
        public void FaceChoices_ComeFromTheCatalogsFaces()
        {
            var catalog = MakeCatalog("mouse");
            catalog.ConfigureForTesting(catalog.Kinds, null, null, new[]
            {
                new FaceStyle("default", "Default", null, null),
                new FaceStyle("lashes", "Lashes", null, null),
            });

            var choices = AppearanceChoices.For(AppearancePart.Face, catalog, MakeOptions());

            Assert.AreEqual(2, choices.Count);
            Assert.AreEqual("lashes", choices[1].id);
            Assert.AreEqual("Lashes", choices[1].displayName);
            Assert.AreEqual("default", AppearanceChoices.Defaults(catalog, MakeOptions()).face);
        }

        [Test]
        public void ProfileService_SavesAndLoadsTheProfileAsJson()
        {
            var saved = new PlayerProfileService();
            saved.Set(new PlayerProfile { name = "Bryn", critterKind = "raccoon", furColor = "black", top = "tunic" });

            var loaded = new PlayerProfileService();
            PlayerProfile announced = null;
            loaded.Changed += profile => announced = profile;
            loaded.RestoreState(saved.CaptureState());

            Assert.IsTrue(loaded.HasProfile);
            Assert.AreEqual("Bryn", loaded.Profile.name);
            Assert.AreEqual("raccoon", loaded.Profile.critterKind);
            Assert.AreEqual("black", loaded.Profile.furColor);
            Assert.AreEqual("tunic", announced.top);
        }

        [Test]
        public void ProfileService_HandsOutCopies()
        {
            var service = new PlayerProfileService();
            service.Set(new PlayerProfile { critterKind = "fox" });

            service.Profile.critterKind = "mouse";

            Assert.AreEqual("fox", service.Profile.critterKind);
        }
    }
}
