using Toolsmith.Config;
using Toolsmith.Tests.Support;

namespace Toolsmith.Tests.Config;

//Swing speed and the chance a handle takes damage. The two differ in how their terms combine - speed adds,
//wear multiplies - and each test says which rule it is pinning.
public class SpeedAndWearTests {

    private const float Tolerance = 0.0001f;

    //TESTING.md section 7. A metal handle's tier contributes nothing to speed; the whole figure comes from the
    //metal, which is what lets one metal differ from another.
    [Theory]
    [InlineData(-0.15f, -0.15f)] //iron and steel
    [InlineData(0.05f, 0.05f)]   //meteoric iron, the one metal that swings faster than neutral
    [InlineData(-0.37f, -0.37f)] //gold, nearly twice iron's density
    public void MetalHandleSpeedComesFromItsMetal(float materialSpeedBonus, float expected) {
        var speed = ToolsmithPartStatsHelpers.CalculateSpeedBonus(
            StatFixtures.Metal(), StatFixtures.PlainGrip(), StatFixtures.Material(1.0f, 0.0f, materialSpeedBonus));

        Assert.Equal(expected, speed, Tolerance);
    }

    //Wood species does not affect speed - every wood is 0.0 - so a wooden handle's figure is its tier alone.
    [Fact]
    public void WoodenHandleSpeedComesFromItsTier() {
        Assert.Equal(0.1f, ToolsmithPartStatsHelpers.CalculateSpeedBonus(
            StatFixtures.Professional(), StatFixtures.PlainGrip(), StatFixtures.Oak()), Tolerance);
        Assert.Equal(0.05f, ToolsmithPartStatsHelpers.CalculateSpeedBonus(
            StatFixtures.Carved(), StatFixtures.PlainGrip(), StatFixtures.Oak()), Tolerance);
    }

    //The three terms add rather than multiply, which is what lets a good grip offset a metal's penalty instead
    //of the penalty being an inescapable tax. Meteoric iron plus a sturdy grip reads +35% in section 7.
    [Fact]
    public void HandleGripAndMaterialSpeedAddTogether() {
        var sturdyGrip = StatFixtures.Grip(0.3f, 1.0f);

        var speed = ToolsmithPartStatsHelpers.CalculateSpeedBonus(
            StatFixtures.Metal(), sturdyGrip, StatFixtures.MeteoricIron());

        Assert.Equal(0.35f, speed, Tolerance);
    }

    //A grip can carry a penalising material back above neutral. Stated separately from the sum above because
    //this is the design intent the additive rule exists to serve.
    [Fact]
    public void AGoodGripCanOffsetAMaterialPenalty() {
        var strongGrip = StatFixtures.Grip(0.25f, 1.0f);

        var speed = ToolsmithPartStatsHelpers.CalculateSpeedBonus(
            StatFixtures.Metal(), strongGrip, StatFixtures.Iron());

        Assert.True(speed > 0.0f);
    }

    //The material argument is optional, and omitting it must mean "no material term" rather than shifting the
    //result - the crude handle is made from firewood and has no wood variant at all.
    [Fact]
    public void OmittingTheMaterialLeavesHandleAndGripUnchanged() {
        var grip = StatFixtures.Grip(0.2f, 1.0f);

        var withoutMaterial = ToolsmithPartStatsHelpers.CalculateSpeedBonus(StatFixtures.Professional(), grip);
        var withNeutral = ToolsmithPartStatsHelpers.CalculateSpeedBonus(
            StatFixtures.Professional(), grip, StatFixtures.NeutralMaterial());

        Assert.Equal(withoutMaterial, withNeutral, Tolerance);
    }

    //Wear: a grip and a treatment each remove a share of what still gets through, so they multiply. A 50% grip
    //and a 50% treatment leave a quarter, not nothing.
    [Fact]
    public void GripAndTreatmentSavesMultiplyRatherThanAdd() {
        var chance = ToolsmithPartStatsHelpers.CalculateGripChanceToDamage(
            StatFixtures.Grip(0.5f, 0.5f), StatFixtures.Treatment(0.0f, chanceToDamageReduction: 0.5f));

        Assert.Equal(0.25f, chance, Tolerance);
    }

    //The consequence of multiplying: no pairing can reach a guaranteed save while each term is below 1.
    [Fact]
    public void StackedSavesNeverReachCertainty() {
        var chance = ToolsmithPartStatsHelpers.CalculateGripChanceToDamage(
            StatFixtures.Grip(0.0f, 0.9f), StatFixtures.Treatment(0.0f, chanceToDamageReduction: 0.9f));

        Assert.True(chance > 0.0f);
    }

    //Greased reads 4% in section 4, applied to a plain grip that saves nothing on its own.
    [Fact]
    public void ATreatmentAloneRemovesItsOwnShare() {
        var chance = ToolsmithPartStatsHelpers.CalculateGripChanceToDamage(
            StatFixtures.PlainGrip(), StatFixtures.Treatment(0.0f, chanceToDamageReduction: 0.04f));

        Assert.Equal(0.96f, chance, Tolerance);
    }

    //An untreated handle is the common case and must come back as the grip's own figure untouched.
    [Fact]
    public void NoTreatmentLeavesTheGripChanceAlone() {
        var grip = StatFixtures.Grip(0.0f, 0.7f);

        Assert.Equal(0.7f, ToolsmithPartStatsHelpers.CalculateGripChanceToDamage(grip), Tolerance);
        Assert.Equal(0.7f, ToolsmithPartStatsHelpers.CalculateGripChanceToDamage(grip, StatFixtures.NoTreatment()), Tolerance);
    }
}
