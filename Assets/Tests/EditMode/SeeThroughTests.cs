using System.Linq;
using BellwortBurrow.Systems.World;
using NUnit.Framework;
using UnityEngine;

namespace BellwortBurrow.Tests.EditMode
{
    public class SeeThroughTests
    {
        // A support with its feet at y 10 and its back at 11.8: a base 4 wide and 2.3 tall, a beam 4 wide up to y 41.
        static readonly Rect Base = new Rect(8f, 10f, 4f, 2.3f), Beam = new Rect(8f, 11.5f, 4f, 29.5f);
        const float Back = 11.8f;

        static Rect Body(float x, float y) => new Rect(x - 0.75f, y, 1.5f, 3f);

        [Test]
        public void BehindTheBeam_ItCoversThePlayer_EvenByAnEdge()
        {
            Assert.IsTrue(SeeThrough.Covers(new Vector2(10f, 14f), Body(10f, 14f), Back, Beam));
            Assert.IsTrue(SeeThrough.Covers(new Vector2(12.6f, 20f), Body(12.6f, 20f), Back, Beam), "an ear or tail behind its edge");
        }

        [Test]
        public void ToTheSide_InFront_OrAboveIt_ItDoesNot()
        {
            Assert.IsFalse(SeeThrough.Covers(new Vector2(14f, 14f), Body(14f, 14f), Back, Beam), "clear of its side");
            Assert.IsFalse(SeeThrough.Covers(new Vector2(10f, 9f), Body(10f, 9f), Back, Beam), "in front of the support");
            Assert.IsFalse(SeeThrough.Covers(new Vector2(10f, 42f), Body(10f, 42f), Back, Beam), "past its top");
        }

        [Test]
        public void TheBeamShowsAsMuchAsItsBaseIsOnTheScreen()
        {
            Assert.AreEqual(1f, SeeThrough.Shown(new Rect(0f, 5f, 30f, 17f), Base), 1e-4f, "all of the base on screen");
            Assert.AreEqual(0.5f, SeeThrough.Shown(new Rect(0f, 11.15f, 30f, 17f), Base), 1e-4f, "half of it");
            Assert.AreEqual(0f, SeeThrough.Shown(new Rect(0f, 13f, 30f, 17f), Base), 1e-4f, "off the bottom");
        }

        [Test]
        public void TheDitherAddsOnePixelInSixteenAStep()
        {
            var thresholds = Enumerable.Range(0, 16).Select(i => SeeThrough.DitherThreshold(i % 4, i / 4)).OrderBy(t => t);
            CollectionAssert.AreEqual(Enumerable.Range(0, 16), thresholds, "each threshold once in every 4 by 4 square");
            Assert.AreEqual(SeeThrough.DitherThreshold(1, 2), SeeThrough.DitherThreshold(5, 6), "the pattern repeats every 4 pixels");
            Assert.AreEqual(0, SeeThrough.StepFor(0f));
            Assert.AreEqual(8, SeeThrough.StepFor(0.5f));
            Assert.AreEqual(SeeThrough.DitherSteps, SeeThrough.StepFor(1f));
        }
    }
}
