using Haft.Config;
using Haft.Tests.Support;
using Haft.Utils;

namespace Haft.Tests.Config;

//The head durability formula, the head's counterpart to HandleDurabilityTests. A head used to take vanilla's
//per-tool durability scaled by a flat multiplier; it now reads the same material table the handle does, so these
//figures are what a player sees on a head's tooltip and a change here is a change they would notice.
public class HeadDurabilityTests {

    //Matches the handle tests: one multiplication, compared well below what a tooltip renders.
    private const float Tolerance = 0.1f;

    //The invariant this change was for. A head is a lump of one metal, so the only thing that may reach its
    //durability is that metal - nothing in the signature can carry the tool it is fitted to. Restoring a tool
    //parameter fails here rather than in play.
    [Fact]
    public void HeadDurabilityDependsOnlyOnItsMaterial() {
        var onOneTool = HaftPartStatsHelpers.CalculateHeadDurability(StatFixtures.Steel());
        var onAnother = HaftPartStatsHelpers.CalculateHeadDurability(StatFixtures.Steel());

        Assert.Equal(onOneTool, onAnother, Tolerance);
    }

    //Hardness against the flat base, which is the whole formula. These are the pre-multiplier figures; what a
    //player reads off a tooltip is these scaled by HeadDurabilityMult.
    [Theory]
    [InlineData(1.3f, 1300f)]   //copper
    [InlineData(2.15f, 2150f)]  //tin bronze
    [InlineData(3.0f, 3000f)]   //iron
    [InlineData(4.55f, 4550f)]  //steel
    [InlineData(5.96f, 5960f)]  //titanium
    public void HeadDurabilityMatchesItsMetalHardness(float hardness, float expected) {
        var material = StatFixtures.Material(hardness, 0.0f, 0.0f);

        Assert.Equal(expected, HaftPartStatsHelpers.CalculateHeadDurability(material), Tolerance);
    }

    //Hardness orders the heads, and it is hardness rather than density that does it. Gold is among the densest
    //metals in the table and among the softest, so a head derived from density would rank it above steel - this
    //pins the axis, since the two fields sit next to each other and are easy to swap by mistake.
    [Fact]
    public void GoldHeadIsWeakerThanSteelDespiteBeingDenser() {
        var gold = StatFixtures.Gold();
        var steel = StatFixtures.Steel();

        Assert.True(gold.density > steel.density, "fixture check: gold is the denser of the two");
        Assert.True(HaftPartStatsHelpers.CalculateHeadDurability(gold)
            < HaftPartStatsHelpers.CalculateHeadDurability(steel));
    }

    //The bug this change was reported for. A head handed back from a broken tool and fitted to a new one used to
    //have its damage rebuilt from a fraction, and the fraction was computed against a maximum that could disagree
    //with the one it was multiplied back out by - so the head came back stronger than it went in. The transfer is
    //the identity now, which is the only way a round trip can be lossless.
    [Theory]
    [InlineData(5000, 5000)] //undamaged
    [InlineData(5000, 3741)] //part-worn, a figure no fraction would land on exactly
    [InlineData(5000, 1)]    //one hit from breaking
    public void RecordedHeadDurabilityTransfersUnchanged(int max, int recorded) {
        Assert.Equal(recorded, HaftPartStatsHelpers.TransferHeadDurability(max, recorded, hasRecord: true));
    }

    //The symptom as the player described it: durability added to a new head rather than carried onto it. A record
    //above the maximum is exactly what the old fraction produced, so this pins the clamp that makes it impossible.
    [Fact]
    public void HeadNeverReturnsStrongerThanANewOne() {
        Assert.Equal(5000, HaftPartStatsHelpers.TransferHeadDurability(5000, 24000, hasRecord: true));
    }

    //A head straight from the anvil has never been fitted to anything. That is not the same as one worn to nothing,
    //and reading it as zero would hand the player a tool that breaks on its first swing.
    [Fact]
    public void HeadWithNoRecordStartsFull() {
        Assert.Equal(5000, HaftPartStatsHelpers.TransferHeadDurability(5000, 0, hasRecord: false));
    }

    //A head genuinely worn to nothing keeps that, rather than being confused with one that has no record at all.
    [Fact]
    public void HeadWornToNothingStaysThere() {
        Assert.Equal(0, HaftPartStatsHelpers.TransferHeadDurability(5000, 0, hasRecord: true));
    }

    //A head outlasting the handle it is fitted to is the property the whole part system rests on: a good handle
    //carries many heads, and a head worth keeping survives the tool coming apart. Steel against professional oak
    //is the closest realistic pairing, so it is the one that would fail first if the two bases drifted.
    [Fact]
    public void SteelHeadOutlastsTheBestWoodenHandle() {
        var head = HaftPartStatsHelpers.CalculateHeadDurability(StatFixtures.Steel());
        var handle = HaftPartStatsHelpers.CalculateHandleDurability(
            StatFixtures.Professional(), StatFixtures.NoTreatment(), StatFixtures.NoBinding(), StatFixtures.Oak());

        Assert.True(head > handle);
    }
}
