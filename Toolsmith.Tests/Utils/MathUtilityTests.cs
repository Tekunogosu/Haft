using Toolsmith.Tests.Support;
using Toolsmith.Utils;

namespace Toolsmith.Tests.Utils;

//The wear and reforging math. The two chance functions are probabilistic, so each is driven with a scripted
//roll either side of its own threshold rather than sampled - a test that rolled genuinely would be reporting
//the random number generator, not the formula.
public class MathUtilityTests {

    private const double Tolerance = 0.0001;

    //The honing damage multiplier over its stated 20%-40% range. A straight line, so its endpoints fix it.
    [Theory]
    [InlineData(0.2f, 2.0)]
    [InlineData(0.3f, 1.5)]
    [InlineData(0.4f, 1.0)]
    public void LinearDamageMultiplierFallsAcrossTheHoningRange(float totalSharpened, double expected) {
        Assert.Equal(expected, MathUtility.GetLinearDamageMult(totalSharpened), Tolerance);
    }

    //More total honing means less damage per hone, which is the direction the whole curve exists to express.
    [Fact]
    public void MoreHoningMeansLessDamagePerHone() {
        Assert.True(MathUtility.GetLinearDamageMult(0.4f) < MathUtility.GetLinearDamageMult(0.2f));
    }

    //The sharpening damage chance clamps to a certainty at and below 40% honed, whatever the roll.
    [Fact]
    public void SharpeningAlwaysDamagesAtOrBelowFortyPercentHoned() {
        var world = FakeWorld.WithRandom(new ScriptedRandom(0.999));

        Assert.True(MathUtility.ShouldDamageFromSharpening(world, 0.4f));
        Assert.True(MathUtility.ShouldDamageFromSharpening(world, 0.3f));
    }

    //And clamps to a 5% floor above 50%, so a well-honed head still carries some risk rather than none.
    [Fact]
    public void SharpeningKeepsAFivePercentFloorWhenWellHoned() {
        Assert.True(MathUtility.ShouldDamageFromSharpening(FakeWorld.WithRandom(new ScriptedRandom(0.04)), 0.9f));
        Assert.False(MathUtility.ShouldDamageFromSharpening(FakeWorld.WithRandom(new ScriptedRandom(0.06)), 0.9f));
    }

    //Between the clamps the chance is the line itself: at 45% honed it works out to 0.525, so a roll either
    //side of that decides it.
    [Fact]
    public void SharpeningChanceFollowsTheLineBetweenTheClamps() {
        Assert.True(MathUtility.ShouldDamageFromSharpening(FakeWorld.WithRandom(new ScriptedRandom(0.52)), 0.45f));
        Assert.False(MathUtility.ShouldDamageFromSharpening(FakeWorld.WithRandom(new ScriptedRandom(0.53)), 0.45f));
    }

    //Below 20% sharpness the defect chance is a flat 0.001 floor rather than the curve, scaled by maxSharp.
    //At the 3500 that scaling is written against, the floor comes through unchanged.
    [Fact]
    public void DefectChanceUsesAFlatFloorBelowTwentyPercentSharp() {
        Assert.True(MathUtility.ShouldChanceForDefectCurve(FakeWorld.WithRandom(new ScriptedRandom(0.0009)), 0.1f, 3500));
        Assert.False(MathUtility.ShouldChanceForDefectCurve(FakeWorld.WithRandom(new ScriptedRandom(0.0011)), 0.1f, 3500));
    }

    //A larger maxSharp divides the chance down, so the same head is less defect-prone the more sharpness it
    //holds. This is the term that keeps the curve meaningful across tools of different quality.
    [Fact]
    public void AHigherMaxSharpnessLowersTheDefectChance() {
        //0.0009 lands under the floor at maxSharp 3500 but over it once maxSharp doubles and halves the chance.
        Assert.True(MathUtility.ShouldChanceForDefectCurve(FakeWorld.WithRandom(new ScriptedRandom(0.0009)), 0.1f, 3500));
        Assert.False(MathUtility.ShouldChanceForDefectCurve(FakeWorld.WithRandom(new ScriptedRandom(0.0009)), 0.1f, 7000));
    }

    //The curve is a parabola centred on 0.8 sharpness, so defects are rarest at the vertex and grow as a head
    //moves away from it. Probed with a roll sitting between the two chances rather than at zero: the squared
    //term at the vertex is float residue rather than a true zero, so a roll of zero still falls under it.
    [Fact]
    public void DefectChanceIsLowestAtTheCurveVertex() {
        //0.0002 is above the vertex's own chance and below the chance half a turn away from it.
        Assert.False(MathUtility.ShouldChanceForDefectCurve(FakeWorld.WithRandom(new ScriptedRandom(0.0002)), 0.8f, 3500));
        Assert.True(MathUtility.ShouldChanceForDefectCurve(FakeWorld.WithRandom(new ScriptedRandom(0.0002)), 0.3f, 3500));
    }

    //Either side of the vertex the chance rises symmetrically - a head honed past the sweet spot is as
    //defect-prone as one the same distance short of it.
    [Fact]
    public void DefectChanceRisesOnBothSidesOfTheVertex() {
        //0.6 and 1.0 sit equally far from the 0.8 vertex, so one roll decides both the same way.
        Assert.True(MathUtility.ShouldChanceForDefectCurve(FakeWorld.WithRandom(new ScriptedRandom(0.0001)), 0.6f, 3500));
        Assert.True(MathUtility.ShouldChanceForDefectCurve(FakeWorld.WithRandom(new ScriptedRandom(0.0001)), 1.0f, 3500));
    }

    //Flooring a use time to a whole number of intervals. The exact multiple must stay put rather than dropping
    //an interval, which is the boundary this kind of arithmetic gets wrong.
    [Theory]
    [InlineData(0.8f, 0.4f, 0.8f)]
    [InlineData(0.79f, 0.4f, 0.4f)]
    [InlineData(0.39f, 0.4f, 0.0f)]
    [InlineData(1.2f, 0.4f, 1.2f)]
    public void FloorToNearestMultiple(float secondsUsed, float mult, float expected) {
        Assert.Equal(expected, MathUtility.FloorToNearestMult(secondsUsed, mult), 0.0001f);
    }

    //Reforging returns a share of the voxels, never fewer than 66% and never more than all of them, so a
    //ruined tool still gives something back and a pristine one gives no bonus.
    [Theory]
    [InlineData(0.0f, 66)]
    [InlineData(1.0f, 100)]
    [InlineData(0.5f, 84)]
    public void ReforgeVoxelsScaleWithRemainingDurability(float damagePercent, int expected) {
        Assert.Equal(expected, MathUtility.NumberOfVoxelsLeftInReforge(damagePercent, 100));
    }

    //The clamps hold outside the range the curve was fitted over, so an out-of-range input cannot return more
    //voxels than the tool had or a negative count.
    [Theory]
    [InlineData(2.0f, 100)]
    [InlineData(-1.0f, 66)]
    public void ReforgeVoxelsStayWithinTheirClamps(float damagePercent, int expected) {
        Assert.Equal(expected, MathUtility.NumberOfVoxelsLeftInReforge(damagePercent, 100));
    }
}
