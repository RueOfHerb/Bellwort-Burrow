using BellwortBurrow.Systems.World;
using NUnit.Framework;

namespace BellwortBurrow.Tests.EditMode
{
    public class SeeThroughTests
    {
        // A support whose back is at y 10 and whose beam's top is at y 41, fading two tiles in.
        const float Back = 10f, Top = 41f, After = 2f;

        [Test]
        public void JustNorthOfIt_ThePillarIsSeeThrough()
        {
            Assert.AreEqual(SeeThrough.Look.SeeThrough, SeeThrough.LookFor(11.5f, Back, Top, After), "a tile and a half in");
        }

        [Test]
        public void PastTheTiles_ThePillarIsGone_AllTheWayUpToItsTop()
        {
            Assert.AreEqual(SeeThrough.Look.Gone, SeeThrough.LookFor(12.5f, Back, Top, After), "past two tiles");
            Assert.AreEqual(SeeThrough.Look.Gone, SeeThrough.LookFor(40f, Back, Top, After), "just under its top");
        }

        [Test]
        public void SouthOfItOrAboveItsTop_ThePillarIsSolid()
        {
            Assert.AreEqual(SeeThrough.Look.Solid, SeeThrough.LookFor(9f, Back, Top, After), "in front of the support");
            Assert.AreEqual(SeeThrough.Look.Solid, SeeThrough.LookFor(42f, Back, Top, After), "past its top");
        }
    }
}
