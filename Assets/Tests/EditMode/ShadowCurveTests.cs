using NUnit.Framework;
using BellwortBurrow.Systems.World;

public class ShadowCurveTests
{
    [Test]
    public void LongestAtRiseAndSet_ShortestHalfway()
    {
        float rise = ShadowCurve.ScaleAt(0f, 15f, 70f), mid = ShadowCurve.ScaleAt(0.5f, 15f, 70f), set = ShadowCurve.ScaleAt(1f, 15f, 70f);
        Assert.That(rise, Is.EqualTo(set).Within(1e-4f));
        Assert.That(mid, Is.LessThan(rise));
        Assert.That(ShadowCurve.ScaleAt(0.25f, 15f, 70f), Is.GreaterThan(mid).And.LessThan(rise));
    }

    [Test]
    public void ScaleIsOneAtFortyFiveDegrees()
    {
        Assert.That(ShadowCurve.ScaleAt(0.5f, 45f, 45f), Is.EqualTo(1f).Within(1e-4f));
    }

    [Test]
    public void PivotsClockwiseAtRise_StraightHalfway_CounterclockwiseAtSet()
    {
        Assert.That(ShadowCurve.AngleAt(0f, 20f), Is.EqualTo(20f).Within(1e-4f));
        Assert.That(ShadowCurve.AngleAt(0.5f, 20f), Is.EqualTo(0f).Within(1e-4f));
        Assert.That(ShadowCurve.AngleAt(1f, 20f), Is.EqualTo(-20f).Within(1e-4f));
    }

    [Test]
    public void TravelWrapsPastMidnight()
    {
        Assert.That(ShadowCurve.Travel(19f, 19f, 5f), Is.EqualTo(0f).Within(1e-4f));
        Assert.That(ShadowCurve.Travel(0f, 19f, 5f), Is.EqualTo(0.5f).Within(1e-4f));
        Assert.That(ShadowCurve.Travel(5f, 19f, 5f), Is.EqualTo(1f).Within(1e-4f));
        Assert.That(ShadowCurve.Travel(8f, 7f, 9f), Is.EqualTo(0.5f).Within(1e-4f));
    }
}
