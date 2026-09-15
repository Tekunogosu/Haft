using Haft.Utils;

namespace Haft.Tests.Utils;

//The percentage-to-color banding every tooltip line shares. One owner means a head, a handle, a binding and a
//sharpness bar all change color at the same thresholds, so these pin the boundaries rather than the midpoints -
//a band is only ever got wrong at its edge.
public class TooltipColorTests {

    //Pristine is reserved for a part that has taken no damage at all, so the boundary is inclusive at exactly
    //full and the very next value below it is merely good.
    [Fact]
    public void OnlyAFullyUndamagedPartIsPristine() {
        Assert.Equal(StringHelpers.PristineColor, StringHelpers.ColorForRemainingPercent(1.0f));
        Assert.Equal(StringHelpers.GoodColor, StringHelpers.ColorForRemainingPercent(0.999f));
    }

    [Theory]
    [InlineData(0.67f)]
    [InlineData(0.9f)]
    public void AboveTwoThirdsIsGood(float remaining) {
        Assert.Equal(StringHelpers.GoodColor, StringHelpers.ColorForRemainingPercent(remaining));
    }

    //Each threshold is exclusive: a part sitting exactly on 0.66 has dropped out of good into worn, and one on
    //0.33 out of worn into poor.
    [Theory]
    [InlineData(0.66f)]
    [InlineData(0.5f)]
    [InlineData(0.34f)]
    public void BetweenAThirdAndTwoThirdsIsWorn(float remaining) {
        Assert.Equal(StringHelpers.WornColor, StringHelpers.ColorForRemainingPercent(remaining));
    }

    [Theory]
    [InlineData(0.33f)]
    [InlineData(0.1f)]
    [InlineData(0.0f)]
    public void AtOrBelowAThirdIsPoor(float remaining) {
        Assert.Equal(StringHelpers.PoorColor, StringHelpers.ColorForRemainingPercent(remaining));
    }

    //A part whose stats have not been written yet carries a max of zero. The guard exists so that reads as
    //unknown rather than dividing by zero.
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ADurabilityWithNoMaxIsUnknown(int max) {
        Assert.Equal(StringHelpers.UnknownColor, StringHelpers.ColorForDurability(500, max));
    }

    [Fact]
    public void DurabilityBandsOnItsCurrentOverMax() {
        Assert.Equal(StringHelpers.PristineColor, StringHelpers.ColorForDurability(1000, 1000));
        Assert.Equal(StringHelpers.GoodColor, StringHelpers.ColorForDurability(800, 1000));
        Assert.Equal(StringHelpers.WornColor, StringHelpers.ColorForDurability(500, 1000));
        Assert.Equal(StringHelpers.PoorColor, StringHelpers.ColorForDurability(100, 1000));
    }

    //A stat line is not a fraction of anything, so it bands by sign against zero. Zero itself is worth neither
    //color, which is what separates this from the multiplier case below.
    [Fact]
    public void ABonusBandsBySign() {
        Assert.Equal(StringHelpers.GoodColor, StringHelpers.ColorForBonus(0.1));
        Assert.Equal(StringHelpers.PoorColor, StringHelpers.ColorForBonus(-0.1));
        Assert.Equal(StringHelpers.UnknownColor, StringHelpers.ColorForBonus(0.0));
    }

    //A multiplier is measured against 1.0, not zero - a multiplier of zero is a total loss and must read poor,
    //which is the case that would break if the two shared one comparison.
    [Fact]
    public void AMultiplierBandsAgainstOne() {
        Assert.Equal(StringHelpers.GoodColor, StringHelpers.ColorForMultiplier(1.5));
        Assert.Equal(StringHelpers.PoorColor, StringHelpers.ColorForMultiplier(0.5));
        Assert.Equal(StringHelpers.UnknownColor, StringHelpers.ColorForMultiplier(1.0));
        Assert.Equal(StringHelpers.PoorColor, StringHelpers.ColorForMultiplier(0.0));
    }

    //Mining speed bands on absolute figures rather than a fraction, and 1x is the bare-hands baseline vanilla
    //filters out - so anything at the bottom band reads unknown rather than poor.
    [Theory]
    [InlineData(5.0f, "pristine")]
    [InlineData(6.0f, "pristine")]
    [InlineData(4.9f, "good")]
    [InlineData(3.0f, "good")]
    [InlineData(2.9f, "worn")]
    [InlineData(1.5f, "worn")]
    [InlineData(1.4f, "unknown")]
    [InlineData(1.0f, "unknown")]
    public void MiningSpeedBandsOnAbsoluteSpeed(float speed, string expectedBand) {
        var expected = expectedBand switch {
            "pristine" => StringHelpers.PristineColor,
            "good" => StringHelpers.GoodColor,
            "worn" => StringHelpers.WornColor,
            _ => StringHelpers.UnknownColor
        };

        Assert.Equal(expected, StringHelpers.ColorForMiningSpeed(speed));
    }
}
