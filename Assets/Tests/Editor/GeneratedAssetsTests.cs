using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using BellwortBurrow.EditorTools;

namespace BellwortBurrow.Tests.Editor
{
    /// <summary>The fingerprint check that keeps setup (and the one-off scene builder) from writing over hand edits.</summary>
    public class GeneratedAssetsTests
    {
        const string Folder = "Assets/BellwortTestTemp";

        [SetUp]
        public void CreateFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "BellwortTestTemp");
        }

        [TearDown]
        public void DeleteFolder()
        {
            AssetDatabase.DeleteAsset(Folder);
        }

        static string MakePrefab(string name, bool stamp)
        {
            string path = $"{Folder}/{name}.prefab";
            var go = new GameObject(name);
            go.AddComponent<SpriteRenderer>();
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            if (stamp) GeneratedAssets.Stamp(path);
            return path;
        }

        static void EditByHand(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            root.AddComponent<BoxCollider2D>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        [Test]
        public void CanOverwrite_NothingThereYet_IsTrue()
        {
            Assert.IsTrue(GeneratedAssets.CanOverwrite($"{Folder}/not_made_yet.prefab"));
        }

        [Test]
        public void CanOverwrite_UnchangedSinceStamped_IsTrue()
        {
            string path = MakePrefab("stamped", stamp: true);
            Assert.IsTrue(GeneratedAssets.CanOverwrite(path));
            Assert.IsFalse(GeneratedAssets.ChangedByHand(path));
        }

        [Test]
        public void CanOverwrite_EditedByHandAfterStamp_IsFalse()
        {
            string path = MakePrefab("edited", stamp: true);
            EditByHand(path);
            Assert.IsFalse(GeneratedAssets.CanOverwrite(path));
            Assert.IsTrue(GeneratedAssets.ChangedByHand(path));
        }

        [Test]
        public void CanOverwrite_NeverStamped_IsFalse()
        {
            // A scene or prefab no tool made (or made before stamping existed) may be someone's work.
            string path = MakePrefab("unstamped", stamp: false);
            Assert.IsFalse(GeneratedAssets.CanOverwrite(path));
            Assert.IsFalse(GeneratedAssets.ChangedByHand(path));
        }

        [Test]
        public void Stamp_AfterHandEdit_LetsSetupRebuildAgain()
        {
            string path = MakePrefab("restamped", stamp: true);
            EditByHand(path);
            GeneratedAssets.Stamp(path);
            Assert.IsTrue(GeneratedAssets.CanOverwrite(path));
        }

        [Test]
        public void Fingerprint_IgnoresWindowsLineEndings()
        {
            string unix = Path.GetTempFileName(), windows = Path.GetTempFileName(), other = Path.GetTempFileName();
            try
            {
                File.WriteAllText(unix, "a: 1\nb: 2\n");
                File.WriteAllText(windows, "a: 1\r\nb: 2\r\n");
                File.WriteAllText(other, "a: 1\nb: 3\n");
                Assert.AreEqual(GeneratedAssets.Fingerprint(unix), GeneratedAssets.Fingerprint(windows));
                Assert.AreNotEqual(GeneratedAssets.Fingerprint(unix), GeneratedAssets.Fingerprint(other));
            }
            finally
            {
                File.Delete(unix); File.Delete(windows); File.Delete(other);
            }
        }
    }
}
