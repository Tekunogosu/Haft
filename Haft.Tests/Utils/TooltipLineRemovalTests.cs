using System.Text;
using Haft.Utils;

namespace Haft.Tests.Utils;

//Removing a vanilla tooltip line so the mod can write its own in its place. The work is index arithmetic over a
//buffer, so these cover the positions a line can sit in - first, middle, last - rather than one representative case.
public class TooltipLineRemovalTests {

    private static StringBuilder Tooltip(params string[] lines) => new(string.Join("\n", lines) + "\n");

    [Fact]
    public void RemovesAMatchingLineFromTheMiddle() {
        var tooltip = Tooltip("Attack power: 3", "Durability: 500 / 1000", "Mining speed: 4.5");

        StringHelpers.RemoveTooltipLineStartingWith(tooltip, "Durability:");

        Assert.Equal("Attack power: 3\nMining speed: 4.5\n", tooltip.ToString());
    }

    [Fact]
    public void RemovesTheFirstLine() {
        var tooltip = Tooltip("Durability: 500 / 1000", "Mining speed: 4.5");

        StringHelpers.RemoveTooltipLineStartingWith(tooltip, "Durability:");

        Assert.Equal("Mining speed: 4.5\n", tooltip.ToString());
    }

    [Fact]
    public void RemovesTheLastLine() {
        var tooltip = Tooltip("Attack power: 3", "Durability: 500 / 1000");

        StringHelpers.RemoveTooltipLineStartingWith(tooltip, "Durability:");

        Assert.Equal("Attack power: 3\n", tooltip.ToString());
    }

    //The whole line goes, including the trailing newline, so removing one does not leave a blank line where it was.
    [Fact]
    public void RemovingTheOnlyLineEmptiesTheBuffer() {
        var tooltip = Tooltip("Durability: 500 / 1000");

        StringHelpers.RemoveTooltipLineStartingWith(tooltip, "Durability:");

        Assert.Equal("", tooltip.ToString());
    }

    //A tool that never had the line costs one scan and no special case at the call site, so an absent prefix must
    //leave the buffer exactly as it was.
    [Fact]
    public void AnAbsentPrefixLeavesTheTooltipUntouched() {
        var tooltip = Tooltip("Attack power: 3", "Mining speed: 4.5");
        var before = tooltip.ToString();

        StringHelpers.RemoveTooltipLineStartingWith(tooltip, "Durability:");

        Assert.Equal(before, tooltip.ToString());
    }

    //A null or empty prefix would otherwise match at index zero and take the first line with it.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ANullOrEmptyPrefixRemovesNothing(string? prefix) {
        var tooltip = Tooltip("Attack power: 3", "Durability: 500 / 1000");
        var before = tooltip.ToString();

        StringHelpers.RemoveTooltipLineStartingWith(tooltip, prefix);

        Assert.Equal(before, tooltip.ToString());
    }

    //The prefix is looked up wherever it occurs, then the removal walks back to the line start - so a line whose
    //label is preceded by leading text still goes in full rather than being cut mid-line.
    [Fact]
    public void RemovesTheWholeLineWhenThePrefixIsNotAtItsStart() {
        var tooltip = Tooltip("Attack power: 3", "  Durability: 500 / 1000", "Mining speed: 4.5");

        StringHelpers.RemoveTooltipLineStartingWith(tooltip, "Durability:");

        Assert.Equal("Attack power: 3\nMining speed: 4.5\n", tooltip.ToString());
    }

    //Only the first occurrence is removed - the method takes one line out per call, and a caller wanting both
    //calls it twice.
    [Fact]
    public void RemovesOnlyTheFirstMatchingLine() {
        var tooltip = Tooltip("Durability: 500 / 1000", "Durability: 300 / 600");

        StringHelpers.RemoveTooltipLineStartingWith(tooltip, "Durability:");

        Assert.Equal("Durability: 300 / 600\n", tooltip.ToString());
    }

    //A buffer whose final line has no trailing newline is the case the lineEnd fallback exists for.
    [Fact]
    public void HandlesAFinalLineWithNoTrailingNewline() {
        var tooltip = new StringBuilder("Attack power: 3\nDurability: 500 / 1000");

        StringHelpers.RemoveTooltipLineStartingWith(tooltip, "Durability:");

        Assert.Equal("Attack power: 3\n", tooltip.ToString());
    }
}
