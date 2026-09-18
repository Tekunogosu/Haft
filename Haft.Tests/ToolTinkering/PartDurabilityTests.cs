using Haft.ToolTinkering;

namespace Haft.Tests.ToolTinkering;

//The readout the honing path reads its values through. Read and Write need a live ItemStack and the item registry,
//so what is covered here is the arithmetic the callers branch on - the part that decides whether a stone keeps
//going, stops, or warns the player before breaking what it is honing.
public class PartDurabilityTests {

    private static TinkeringUtility.PartDurability Readout(int curDur, int maxDur, int curSharp, int maxSharp) =>
        new() {
            CurrentDurability = curDur,
            MaxDurability = maxDur,
            CurrentSharpness = curSharp,
            MaxSharpness = maxSharp
        };

    [Theory]
    [InlineData(5000, 5000, 1.0f)]
    [InlineData(2500, 5000, 0.5f)]
    [InlineData(1, 5000, 0.0002f)]
    public void DurabilityPercentIsCurrentOverMax(int curDur, int maxDur, float expected) {
        Assert.Equal(expected, Readout(curDur, maxDur, 0, 0).DurabilityPercent, 0.0001f);
    }

    //An absent maximum reads as zero rather than dividing by it. A stand-in maximum here is what let a head report
    //a fraction above 1.0 and come back stronger than a new one, so the guard belongs in the readout itself.
    [Theory]
    [InlineData(500, 0)]  //no maximum recorded
    [InlineData(0, 5000)] //nothing left
    [InlineData(0, 0)]    //nothing recorded at all
    public void DurabilityPercentIsZeroWhenEitherValueIsMissing(int curDur, int maxDur) {
        Assert.Equal(0.0f, Readout(curDur, maxDur, 0, 0).DurabilityPercent);
    }

    [Fact]
    public void NeedsSharpeningWhenDullAndNotBroken() {
        Assert.True(Readout(5000, 5000, 200, 1000).NeedsSharpening);
    }

    [Fact]
    public void AlreadySharpNeedsNoSharpening() {
        Assert.False(Readout(5000, 5000, 1000, 1000).NeedsSharpening);
    }

    //A tool with no durability left is not honeable however dull it is, which is what stops a stone working on
    //something already broken.
    [Fact]
    public void BrokenToolNeedsNoSharpeningHoweverDull() {
        Assert.False(Readout(0, 5000, 0, 1000).NeedsSharpening);
    }

    //The placeholder-head path returns durability with no sharpness, which has to read as nothing to work on. A
    //default readout reaching NeedsSharpening as true would have the stone hone a tool whose head was never recorded.
    [Fact]
    public void ReadoutWithNoSharpnessRecordedNeedsNoSharpening() {
        Assert.False(Readout(5000, 5000, 0, 0).NeedsSharpening);
    }
}
