using System.IO;
using NUnit.Framework;
using BellwortBurrow.Systems.Save;

namespace BellwortBurrow.Tests.EditMode
{
    public class SaveStoreTests
    {
        string folder;
        SaveStore store;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "BellwortBurrowSaveTests", Path.GetRandomFileName());
            store = new SaveStore(folder);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        static SaveFile MakeFile(string slot, string name)
        {
            var file = new SaveFile { header = new SaveHeader { version = SaveService.CurrentVersion, slot = slot, playerName = name, day = 3 } };
            file.sections.Add(new SaveSection { key = "Profile", json = "{\"name\":\"" + name + "\"}" });
            return file;
        }

        [Test]
        public void Write_ThenRead_GivesTheSameFileBack()
        {
            store.Write("slot1", MakeFile("slot1", "Bryn"));

            Assert.IsTrue(store.TryRead("slot1", out var file));
            Assert.AreEqual("Bryn", file.header.playerName);
            Assert.AreEqual(3, file.header.day);
            Assert.AreEqual("{\"name\":\"Bryn\"}", file.Find("Profile"));
            Assert.IsFalse(File.Exists(store.PathFor("slot1") + ".tmp"));
        }

        [Test]
        public void SecondWrite_KeepsTheFirstAsBackup()
        {
            store.Write("slot1", MakeFile("slot1", "First"));
            store.Write("slot1", MakeFile("slot1", "Second"));

            Assert.IsTrue(store.TryRead("slot1", out var file));
            Assert.AreEqual("Second", file.header.playerName);
            Assert.IsTrue(File.Exists(store.PathFor("slot1") + ".bak"));
        }

        [Test]
        public void BrokenFile_FallsBackToTheBackup()
        {
            store.Write("slot1", MakeFile("slot1", "First"));
            store.Write("slot1", MakeFile("slot1", "Second"));
            File.WriteAllText(store.PathFor("slot1"), "{ this is not a save");

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("backup was loaded"));
            Assert.IsTrue(store.TryRead("slot1", out var file));
            Assert.AreEqual("First", file.header.playerName);
        }

        [Test]
        public void MissingSlot_CantBeRead()
        {
            Assert.IsFalse(store.TryRead("slot9", out _));
        }

        [Test]
        public void FirstFreeSlot_SkipsUsedSlots()
        {
            Assert.AreEqual("slot1", store.FirstFreeSlot());
            store.Write("slot1", MakeFile("slot1", "Bryn"));
            Assert.AreEqual("slot2", store.FirstFreeSlot());
        }

        [Test]
        public void ListSlots_ReadsEveryHeader_DevSlotFirstThenSlotOrder()
        {
            store.Write("slot10", MakeFile("slot10", "Ten"));
            store.Write("slot2", MakeFile("slot2", "Two"));
            store.Write(SaveStore.DevSlot, MakeFile(SaveStore.DevSlot, "Dev"));

            var slots = store.ListSlots();

            Assert.AreEqual(3, slots.Count);
            Assert.AreEqual("Dev", slots[0].playerName);
            Assert.AreEqual("Two", slots[1].playerName);
            Assert.AreEqual("Ten", slots[2].playerName);
        }
    }
}
