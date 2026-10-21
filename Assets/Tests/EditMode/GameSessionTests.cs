using System.IO;
using NUnit.Framework;
using UnityEngine;
using BellwortBurrow.Core;
using BellwortBurrow.Data;
using BellwortBurrow.Systems;
using BellwortBurrow.Systems.Profile;
using BellwortBurrow.Systems.Relationships;
using BellwortBurrow.Systems.Save;

namespace BellwortBurrow.Tests.EditMode
{
    public class GameSessionTests
    {
        string folder;
        SaveStore store;
        CalendarConfig calendar;
        ItemCatalog items;
        ItemDefinition acorn;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "BellwortBurrowSessionTests", Path.GetRandomFileName());
            store = new SaveStore(folder);

            calendar = ScriptableObject.CreateInstance<CalendarConfig>();
            calendar.ConfigureForTesting(daysPerSeason: 28, realSecondsPerInGameMinute: 0.01f, minutesPerHour: 60, hoursPerDay: 24);

            acorn = ScriptableObject.CreateInstance<ItemDefinition>();
            acorn.ConfigureForTesting("acorn");
            items = ScriptableObject.CreateInstance<ItemCatalog>();
            items.ConfigureForTesting(new[] { acorn });
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        GameSession MakeSession() => new(() => new GameServices(calendar, null, items.Find), store);

        static PlayerProfile Bryn() => new() { name = "Bryn", critterKind = "finch", furColor = "finch-feathers" };

        [Test]
        public void NewSession_RegistersEveryServiceAndItself()
        {
            var session = MakeSession();

            Assert.AreSame(session, ServiceLocator.Get<IGameSession>());
            Assert.AreSame(session.Services.Profile, ServiceLocator.Get<IPlayerProfileService>());
            Assert.IsFalse(session.Services.Profile.HasProfile);
            Assert.AreEqual("", session.Slot);
        }

        [Test]
        public void StartNewGame_GivesFreshServicesAndMakesTheFirstSave()
        {
            var session = MakeSession();
            session.Services.Relationships.AddFriendship("Nadia", 30);

            session.StartNewGame(Bryn(), "ForlornForest");

            Assert.AreEqual(0, ServiceLocator.Get<IRelationshipService>().GetFriendship("Nadia"));
            Assert.AreEqual("finch", ServiceLocator.Get<IPlayerProfileService>().Profile.critterKind);
            Assert.AreEqual("slot1", session.Slot);
            Assert.IsTrue(store.TryRead("slot1", out var file));
            Assert.AreEqual("Bryn", file.header.playerName);
            Assert.AreEqual("finch", file.header.critterKind);
            Assert.AreEqual("ForlornForest", file.header.zone);
            Assert.AreEqual(SaveService.CurrentVersion, file.header.version);
        }

        [Test]
        public void SecondNewGame_TakesTheNextSlot()
        {
            var session = MakeSession();
            session.StartNewGame(Bryn(), "ForlornForest");
            session.StartNewGame(Bryn(), "ForlornForest");

            Assert.AreEqual("slot2", session.Slot);
        }

        [Test]
        public void SaveThenLoad_BringsBackTheProfileDayInventoryAndFriends()
        {
            var session = MakeSession();
            session.StartNewGame(Bryn(), "ForlornForest");
            session.Services.Inventory.TryAdd(acorn, 12);
            session.Services.Relationships.AddFriendship("Nadia", 25);
            session.EnterScene("ForlornForest", true);
            session.Tick(8.4f + 0.1f); // one in-game day, 7 PM to 9 AM
            Assert.IsTrue(session.Save());

            var later = MakeSession();
            Assert.IsTrue(later.Load("slot1"));

            Assert.AreEqual("Bryn", later.Services.Profile.Profile.name);
            Assert.AreEqual(2, later.Services.Time.CurrentDay);
            Assert.AreEqual(1, later.Services.Inventory.Slots.Count);
            Assert.AreSame(acorn, later.Services.Inventory.Slots[0].Item);
            Assert.AreEqual(12, later.Services.Inventory.Slots[0].Quantity);
            Assert.AreEqual(25, later.Services.Relationships.GetFriendship("Nadia"));
            Assert.AreEqual("slot1", later.Slot);
            Assert.AreEqual("ForlornForest", later.Zone);
        }

        [Test]
        public void NewGames_ReuseTheDevSlot_WhenOneIsSet()
        {
            var session = MakeSession();
            session.NewGameSlot = SaveStore.DevSlot;

            session.StartNewGame(Bryn(), "ForlornForest");
            session.StartNewGame(new PlayerProfile { name = "Wren", critterKind = "cat" }, "ForlornForest");

            Assert.AreEqual(SaveStore.DevSlot, session.Slot);
            Assert.IsTrue(store.TryRead(SaveStore.DevSlot, out var file));
            Assert.AreEqual("Wren", file.header.playerName);
            Assert.AreEqual("slot1", store.FirstFreeSlot());
        }

        [Test]
        public void Clock_RunsOnlyWhileThePlayerIsInTheWorld()
        {
            var session = MakeSession();
            session.StartNewGame(Bryn(), "ForlornForest");

            session.Tick(1f);
            Assert.AreEqual(calendar.WakeMinute, session.Services.Time.MinutesSinceMidnight, "The clock ran before the player was in the world.");

            session.EnterScene("CharacterCreator", false);
            session.Tick(1f);
            Assert.AreEqual(calendar.WakeMinute, session.Services.Time.MinutesSinceMidnight, "The clock ran outside a zone.");
            Assert.AreEqual(0f, session.PlaySeconds);

            session.EnterScene("ForlornForest", true);
            session.Tick(1f);
            Assert.Greater(session.Services.Time.MinutesSinceMidnight, calendar.WakeMinute);
            Assert.AreEqual(1f, session.PlaySeconds, 0.001f);
            Assert.IsTrue(session.InWorld);
        }

        [Test]
        public void TheEndOfTheDay_WakesThePlayerOnTheNextDayAndSaves()
        {
            var session = MakeSession();
            session.StartNewGame(Bryn(), "ForlornForest");
            session.EnterScene("ForlornForest", true);

            session.Tick(8.4f - 0.05f);
            Assert.AreEqual(1, session.Services.Time.CurrentDay, "The day ended before 9:00 AM.");
            session.Tick(0.1f);

            Assert.AreEqual(2, session.Services.Time.CurrentDay);
            Assert.AreEqual(calendar.WakeMinute, session.Services.Time.MinutesSinceMidnight);
            Assert.IsFalse(session.Services.Time.DayOver);
            Assert.IsTrue(store.TryRead("slot1", out var file));
            Assert.AreEqual(2, file.header.day);
        }

        [Test]
        public void EnteringAScene_SavesAGameInProgress()
        {
            var session = MakeSession();
            session.StartNewGame(Bryn(), "ForlornForest");
            session.Services.Relationships.AddFriendship("Nadia", 7);

            session.EnterScene("ThicketPath", true);

            Assert.IsTrue(store.TryRead("slot1", out var file));
            Assert.AreEqual("ThicketPath", file.header.zone);
            StringAssert.Contains("Nadia", file.Find("Relationships"));
        }

        [Test]
        public void EnteringAScene_WithNoGame_SavesNothing()
        {
            var session = MakeSession();

            session.EnterScene("ForlornForest", true);

            Assert.IsFalse(Directory.Exists(folder));
            Assert.AreEqual("ForlornForest", session.Zone);
        }

        [Test]
        public void Load_RefusesASaveFromANewerGame()
        {
            var newer = new SaveFile { header = new SaveHeader { version = SaveService.CurrentVersion + 1, slot = "slot1" } };
            store.Write("slot1", newer);
            var session = MakeSession();
            var servicesBefore = session.Services;

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("newer version"));
            Assert.IsFalse(session.Load("slot1"));
            Assert.AreSame(servicesBefore, session.Services);
        }

        [Test]
        public void Save_WithNoGameStarted_DoesNothing()
        {
            var session = MakeSession();

            Assert.IsFalse(session.Save());
            Assert.IsFalse(Directory.Exists(folder));
        }

        [Test]
        public void Load_DropsItemsTheCatalogDoesntKnow()
        {
            var session = MakeSession();
            session.StartNewGame(Bryn(), "ForlornForest");
            var stranger = ScriptableObject.CreateInstance<ItemDefinition>();
            stranger.ConfigureForTesting("moonstone");
            session.Services.Inventory.TryAdd(stranger, 1);
            session.Services.Inventory.TryAdd(acorn, 2);
            session.Save();

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("moonstone"));
            Assert.IsTrue(MakeSession().Load("slot1"));

            var inventory = ((GameSession)ServiceLocator.Get<IGameSession>()).Services.Inventory;
            Assert.AreEqual(1, inventory.Slots.Count);
            Assert.AreSame(acorn, inventory.Slots[0].Item);
        }
    }
}
