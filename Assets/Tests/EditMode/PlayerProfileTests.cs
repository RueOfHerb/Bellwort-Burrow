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
                new AppearanceOptionList { part = AppearancePart.FurColor, options = { new AppearanceOption("fox-fur", "Fox fur"), new AppearanceOption("white-fur", "White fur") } },
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
            var choices = AppearanceChoices.For(AppearancePart.Belt, null, MakeOptions());

            Assert.AreEqual("", AppearanceChoices.Step(choices, "", 1));
            Assert.AreEqual(AppearanceChoices.NoChoice, AppearanceChoices.DisplayName(choices, ""));
        }

        [Test]
        public void Defaults_PickTheFirstChoiceOfEveryPart()
        {
            var profile = AppearanceChoices.Defaults(MakeCatalog("mouse", "fox"), MakeOptions());

            Assert.AreEqual("mouse", profile.critterKind);
            Assert.AreEqual("fox-fur", profile.furColor);
            Assert.AreEqual("default", profile.face);
            Assert.AreEqual("", profile.belt);
            Assert.AreEqual("", profile.name);
        }

        [Test]
        public void ProfileService_SavesAndLoadsTheProfileAsJson()
        {
            var saved = new PlayerProfileService();
            saved.Set(new PlayerProfile { name = "Bryn", critterKind = "raccoon", furColor = "black-fur", shirt = "tunic" });

            var loaded = new PlayerProfileService();
            PlayerProfile announced = null;
            loaded.Changed += profile => announced = profile;
            loaded.RestoreState(saved.CaptureState());

            Assert.IsTrue(loaded.HasProfile);
            Assert.AreEqual("Bryn", loaded.Profile.name);
            Assert.AreEqual("raccoon", loaded.Profile.critterKind);
            Assert.AreEqual("black-fur", loaded.Profile.furColor);
            Assert.AreEqual("tunic", announced.shirt);
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
