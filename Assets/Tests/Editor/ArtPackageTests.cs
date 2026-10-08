using System.IO;
using NUnit.Framework;
using BellwortBurrow.EditorTools;

namespace BellwortBurrow.Tests.Editor
{
    public class ArtPackageTests
    {
        static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "BellwortProject"));
        static readonly string Art = Path.Combine(Root, "Assets", "Art");

        [Test]
        public void TargetPath_FileInsideArt_GoesUnderAssetsArt()
        {
            string target = ArtPackage.TargetPath(Root, Art, "Assets/Art/Forest/forest_trees.aseprite");
            Assert.AreEqual(Path.Combine(Art, "Forest", "forest_trees.aseprite"), target);
        }

        [Test]
        public void TargetPath_ArtFolderMeta_IsAllowed()
        {
            Assert.AreEqual(Art + ".meta", ArtPackage.TargetPath(Root, Art, "Assets/Art.meta"));
        }

        [TestCase("Assets/Scripts/Evil.cs")]
        [TestCase("Assets/Artwork/look-alike.png")]
        [TestCase("../outside.txt")]
        [TestCase("Assets/Art/../../escape.txt")]
        [TestCase("ProjectSettings/ProjectSettings.asset")]
        public void TargetPath_AnythingOutsideArt_IsNeverWritten(string entryName)
        {
            Assert.IsNull(ArtPackage.TargetPath(Root, Art, entryName));
        }

        [Test]
        public void Classify_MissingHere_IsAdded()
        {
            Assert.AreEqual(ArtPackage.Change.Add, ArtPackage.Classify(false, null, "pkg", null));
        }

        [Test]
        public void Classify_SameAsPackage_IsLeftAlone()
        {
            Assert.AreEqual(ArtPackage.Change.Same, ArtPackage.Classify(true, "abc", "abc", "old"));
        }

        [Test]
        public void Classify_UntouchedSinceLastPackage_IsUpdated()
        {
            Assert.AreEqual(ArtPackage.Change.Update, ArtPackage.Classify(true, "old", "new", "old"));
        }

        [Test]
        public void Classify_ChangedSinceLastPackage_IsProtected()
        {
            Assert.AreEqual(ArtPackage.Change.ChangedHere, ArtPackage.Classify(true, "mine", "new", "old"));
        }

        [Test]
        public void Classify_NoRecordOfLastPackage_IsProtected()
        {
            Assert.AreEqual(ArtPackage.Change.ChangedHere, ArtPackage.Classify(true, "mine", "new", null));
        }
    }
}
