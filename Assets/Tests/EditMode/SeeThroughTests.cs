using BellwortBurrow.Systems.World;
using NUnit.Framework;
using UnityEngine;

namespace BellwortBurrow.Tests.EditMode
{
    public class SeeThroughTests
    {
        // A support with its feet at y 10 and its back at 11.8: a base 4 wide and 2.3 tall, a beam 4 wide up to y 41,
        // and a screen 30 by 17 that shows the base.
        static readonly Rect Base = new Rect(8f, 10f, 4f, 2.3f), Beam = new Rect(8f, 11.5f, 4f, 29.5f), Screen = new Rect(0f, 5f, 30f, 17f);
        const float Back = 11.8f, Margin = 0.5f, Height = 2f;

        [Test]
        public void BehindTheBeam_ItIsSeeThrough()
        {
            Assert.AreEqual(SeeThrough.Look.SeeThrough, SeeThrough.LookFor(Screen, Base, new Vector2(10f, 14f), Back, Beam, Margin, Height));
            Assert.AreEqual(SeeThrough.Look.SeeThrough, SeeThrough.LookFor(Screen, Base, new Vector2(12.3f, 20f), Back, Beam, Margin, Height), "half behind its edge");
        }

        [Test]
        public void NorthOfItButToTheSide_OrInFront_ItIsSolid()
        {
            Assert.AreEqual(SeeThrough.Look.Solid, SeeThrough.LookFor(Screen, Base, new Vector2(16f, 14f), Back, Beam, Margin, Height), "off to the side");
            Assert.AreEqual(SeeThrough.Look.Solid, SeeThrough.LookFor(Screen, Base, new Vector2(10f, 9f), Back, Beam, Margin, Height), "in front of the support");
            Assert.AreEqual(SeeThrough.Look.Solid, SeeThrough.LookFor(Screen, Base, new Vector2(10f, 42f), Back, Beam, Margin, Height), "past its top");
        }

        [Test]
        public void WithItsBaseOffTheScreen_ItIsGone_WhereverThePlayerIs()
        {
            var above = new Rect(0f, 13f, 30f, 17f);
            Assert.AreEqual(SeeThrough.Look.Gone, SeeThrough.LookFor(above, Base, new Vector2(16f, 20f), Back, Beam, Margin, Height));
            Assert.AreEqual(SeeThrough.Look.Gone, SeeThrough.LookFor(above, Base, new Vector2(10f, 20f), Back, Beam, Margin, Height));
        }
    }
}
