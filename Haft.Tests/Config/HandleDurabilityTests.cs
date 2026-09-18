using Haft.Config;
using Haft.Tests.Support;
using Haft.Utils;

namespace Haft.Tests.Config;

//The handle durability formula. These figures are the ones TESTING.md asks a player to read off a tooltip,
//so a change here is a change a player would notice, and the test names say which number moved.
public class HandleDurabilityTests {

    //Floats accumulate four multiplications before being compared; a tenth of a point is far below anything
    //a tooltip renders, which rounds to a whole number.
    private const float Tolerance = 0.1f;

    //The invariant the whole durability decouple was for: a handle is a length of oak or a bar of steel and
    //knows nothing about the head fixed to it. Nothing in the signature can carry the head's metal, which is
    //what makes the property hold - this pins that, so restoring a head parameter fails here rather than in play.
    [Fact]
    public void HandleDurabilityDependsOnlyOnHandlePartsAndMaterial() {
        var onOneTool = HaftPartStatsHelpers.CalculateHandleDurability(
            StatFixtures.Metal(), StatFixtures.NoTreatment(), StatFixtures.NoBinding(), StatFixtures.Copper());
        var onAnother = HaftPartStatsHelpers.CalculateHandleDurability(
            StatFixtures.Metal(), StatFixtures.NoTreatment(), StatFixtures.NoBinding(), StatFixtures.Copper());

        Assert.Equal(onOneTool, onAnother, Tolerance);
    }

    //The exact figures from TESTING.md section 2. A copper metal handle read ~12000 before the decouple
    //because it was scaled by a steel head; 2990 is that same handle scaled by copper's own hardness instead.
    [Theory]
    [InlineData(1.3f, 2990f)]   //copper
    [InlineData(3.0f, 6900f)]   //iron
    [InlineData(4.55f, 10465f)] //steel
    public void MetalHandleDurabilityMatchesItsMetalHardness(float hardness, float expected) {
        var material = StatFixtures.Material(hardness, 0.0f, 0.0f);

        var durability = HaftPartStatsHelpers.CalculateHandleDurability(
            StatFixtures.Metal(), StatFixtures.NoTreatment(), StatFixtures.NoBinding(), material);

        Assert.Equal(expected, durability, Tolerance);
    }

    //The wooden tiers, same section. Oak is the 1.0 baseline, so these isolate the handle tier's own factor.
    [Fact]
    public void ProfessionalOakHandleDurability() {
        Assert.Equal(1650f, HaftPartStatsHelpers.CalculateHandleDurability(
            StatFixtures.Professional(), StatFixtures.NoTreatment(), StatFixtures.NoBinding(), StatFixtures.Oak()), Tolerance);
    }

    [Fact]
    public void CarvedOakHandleDurability() {
        Assert.Equal(1260f, HaftPartStatsHelpers.CalculateHandleDurability(
            StatFixtures.Carved(), StatFixtures.NoTreatment(), StatFixtures.NoBinding(), StatFixtures.Oak()), Tolerance);
    }

    [Fact]
    public void StickHandleDurability() {
        Assert.Equal(600f, HaftPartStatsHelpers.CalculateHandleDurability(
            StatFixtures.Stick(), StatFixtures.NoTreatment(), StatFixtures.NoBinding(), StatFixtures.Oak()), Tolerance);
    }

    //Density scales the handle's own factor, so it multiplies the base rather than being added at the end.
    [Fact]
    public void DensityScalesDurabilityProportionally() {
        var atOak = HaftPartStatsHelpers.CalculateHandleDurability(
            StatFixtures.Carved(), StatFixtures.NoTreatment(), StatFixtures.NoBinding(), StatFixtures.Material(1.0f, 0f, 0f));
        var atDouble = HaftPartStatsHelpers.CalculateHandleDurability(
            StatFixtures.Carved(), StatFixtures.NoTreatment(), StatFixtures.NoBinding(), StatFixtures.Material(2.0f, 0f, 0f));

        Assert.Equal(atOak * 2.0f, atDouble, Tolerance);
    }

    //The documented compounding rule: each bonus applies to the running total, not to the base. A treatment
    //and a binding worth 0.5 each therefore give 2.25x, not the 2.0x they would if they simply added.
    [Fact]
    public void TreatmentAndBindingBonusesCompoundRatherThanAdd() {
        var handle = StatFixtures.Handle(1.0f, 0.0f, 0.0f, 0.0f);

        var durability = HaftPartStatsHelpers.CalculateHandleDurability(
            handle, StatFixtures.Treatment(0.5f), StatFixtures.Binding(1.0f, 0f, 0.5f), StatFixtures.NeutralMaterial());

        Assert.Equal(HaftConstants.PartDurabilityBase * 2.25f, durability, Tolerance);
    }

    //A handle with no bonuses anywhere should land exactly on the flat base, which is what every other case
    //is a multiple of.
    [Fact]
    public void UnbonusedHandleLandsOnTheFlatBase() {
        var durability = HaftPartStatsHelpers.CalculateHandleDurability(
            StatFixtures.Handle(1.0f, 0.0f, 0.0f, 0.0f), StatFixtures.NoTreatment(),
            StatFixtures.NoBinding(), StatFixtures.NeutralMaterial());

        Assert.Equal(HaftConstants.PartDurabilityBase, durability, Tolerance);
    }
}
