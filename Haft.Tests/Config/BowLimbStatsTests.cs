using Haft.Config;
using Haft.Tests.Support;

namespace Haft.Tests.Config;

//The bow limb axes. Draw weight reads density and flexibility - the mass a limb throws and the springback that
//returns it - and deliberately NOT hardness, which is the handle-wear axis. These tests pin that separation: the
//two order the materials almost oppositely at the metal end, so reading the wrong one is a silent balance change
//rather than a crash.
public class BowLimbStatsTests {

    private const float Tolerance = 0.01f;

    //Oak is the 1.00 baseline on both limb axes by construction, so its draw weight is the unit every other
    //material is read against.
    [Fact]
    public void OakIsTheDrawWeightBaseline() {
        Assert.Equal(1.0f, HaftPartStatsHelpers.CalculateBowDrawWeight(StatFixtures.Oak()), Tolerance);
    }

    //The invariant the density split was for. Gold is the densest material in the table and among the softest:
    //heavy enough to throw an arrow, far too dead to return the energy. Reading hardness here instead would rate
    //a gold limb at 0.55 x 0.1 and a steel one at 4.55 x 0.85 - the same ordering by luck, but for the wrong
    //reason, and it puts gold above lead rather than below it.
    [Fact]
    public void ADenseSoftMetalMakesAPoorLimb() {
        var gold = HaftPartStatsHelpers.CalculateBowDrawWeight(StatFixtures.Gold());
        var steel = HaftPartStatsHelpers.CalculateBowDrawWeight(StatFixtures.Steel());

        Assert.True(gold < steel, $"gold {gold} should draw lighter than steel {steel}");
        Assert.True(gold < 1.0f, $"gold {gold} should draw lighter than oak");
    }

    //Draw weight is the product of the two axes, so a material dense enough to store energy still reads as a poor
    //bow when it cannot return it. Pins the multiply rather than an add: adding would let either axis carry a
    //material on its own.
    [Theory]
    [InlineData(3.12f, 0.10f, 0.312f)] //gold - heavy and dead
    [InlineData(2.27f, 0.85f, 1.930f)] //steel - heavy and springy
    [InlineData(1.00f, 1.00f, 1.000f)] //oak
    public void DrawWeightMultipliesDensityByFlexibility(float density, float flexibility, float expected) {
        var material = StatFixtures.Material(1.0f, 0.0f, 0.0f, density: density, flexibility: flexibility);

        Assert.Equal(expected, HaftPartStatsHelpers.CalculateBowDrawWeight(material), Tolerance);
    }

    //Hardness is the handle-wear axis and must not reach a limb. Two materials with the same limb axes and wildly
    //different hardness have to draw identically, which is what keeps a handle rebalance from silently moving
    //every bow in the game.
    [Fact]
    public void HardnessDoesNotAffectDrawWeight() {
        var soft = StatFixtures.Material(0.55f, 0.0f, 0.0f, density: 2.0f, flexibility: 0.5f);
        var hard = StatFixtures.Material(4.55f, 0.0f, 0.0f, density: 2.0f, flexibility: 0.5f);

        Assert.Equal(
            HaftPartStatsHelpers.CalculateBowDrawWeight(soft),
            HaftPartStatsHelpers.CalculateBowDrawWeight(hard),
            Tolerance);
    }

    //Both limb axes are left unset rather than defaulted so a material nobody has given bow data reads as "not a
    //limb" instead of silently rating as well as oak. The uranium alloys ship exactly this way: a hardness and no
    //flexibility, so they are handle materials until someone gives them limb numbers.
    [Theory]
    [InlineData(-1.0f, 1.0f)]  //no density
    [InlineData(1.0f, -1.0f)]  //no flexibility
    [InlineData(-1.0f, -1.0f)] //neither
    public void AMaterialMissingEitherLimbAxisCannotFormALimb(float density, float flexibility) {
        var material = StatFixtures.Material(1.0f, 0.0f, 0.0f, density: density, flexibility: flexibility);

        Assert.False(HaftPartStatsHelpers.CanMaterialFormLimb(material));
        Assert.Equal(0.0f, HaftPartStatsHelpers.CalculateBowDrawWeight(material), Tolerance);
    }

    [Fact]
    public void ANullMaterialCannotFormALimb() {
        Assert.False(HaftPartStatsHelpers.CanMaterialFormLimb(null));
    }
}
