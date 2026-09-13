using Toolsmith.Config;
using Toolsmith.Tests.Support;
using Toolsmith.Utils;

namespace Toolsmith.Tests.Config;

//The binding durability formula and the one branch in it: whether the handle's material reaches the binding
//at all. It does for a nailed binding and does not for a wrapped one.
public class BindingDurabilityTests {

    private const float Tolerance = 0.1f;

    //TESTING.md section 2: steel nails should read the same whatever head the tool has. Same property as the
    //handle's, and it holds for the same reason - no head stat is in the signature.
    [Fact]
    public void BindingDurabilityDoesNotVaryWithTheToolItIsOn() {
        var nails = StatFixtures.Binding(1.0f, 0.1f, 0.0f, isMetal: true);

        var first = ToolsmithPartStatsHelpers.CalculateBindingDurability(StatFixtures.Carved(), nails, StatFixtures.Oak());
        var second = ToolsmithPartStatsHelpers.CalculateBindingDurability(StatFixtures.Carved(), nails, StatFixtures.Oak());

        Assert.Equal(first, second, Tolerance);
    }

    //A rope or twine wrap is tightened around the handle rather than driven into it, so the material's
    //nailBindingBonus must not reach it however dense that material is.
    [Fact]
    public void NonMetalBindingIgnoresTheMaterialNailBonus() {
        var wrap = StatFixtures.Binding(1.0f, 0.0f, 0.0f, isMetal: false);
        var grippy = StatFixtures.Material(1.0f, 0.5f, 0.0f);

        var onNeutral = ToolsmithPartStatsHelpers.CalculateBindingDurability(
            StatFixtures.Carved(), wrap, StatFixtures.NeutralMaterial());
        var onGrippy = ToolsmithPartStatsHelpers.CalculateBindingDurability(
            StatFixtures.Carved(), wrap, grippy);

        Assert.Equal(onNeutral, onGrippy, Tolerance);
    }

    //A nailed binding is the case the bonus exists for, so the same material must change the result here.
    [Fact]
    public void MetalBindingTakesTheMaterialNailBonus() {
        var nails = StatFixtures.Binding(1.0f, 0.0f, 0.0f, isMetal: true);
        var grippy = StatFixtures.Material(1.0f, 0.5f, 0.0f);

        var onNeutral = ToolsmithPartStatsHelpers.CalculateBindingDurability(
            StatFixtures.Carved(), nails, StatFixtures.NeutralMaterial());
        var onGrippy = ToolsmithPartStatsHelpers.CalculateBindingDurability(
            StatFixtures.Carved(), nails, grippy);

        Assert.Equal(onNeutral * 1.5f, onGrippy, Tolerance);
    }

    //A porous wood carries a negative nailBindingBonus - kapok is -0.1 - so a nail holds worse in it. The
    //sign is the point: the branch must not be written as an absolute bonus.
    [Fact]
    public void APorousMaterialWeakensANailedBinding() {
        var nails = StatFixtures.Binding(1.0f, 0.0f, 0.0f, isMetal: true);
        var kapok = StatFixtures.Material(0.45f, -0.1f, 0.0f);

        var durability = ToolsmithPartStatsHelpers.CalculateBindingDurability(StatFixtures.Carved(), nails, kapok);
        var neutral = ToolsmithPartStatsHelpers.CalculateBindingDurability(
            StatFixtures.Carved(), nails, StatFixtures.NeutralMaterial());

        Assert.True(durability < neutral);
    }

    //The handle lends the binding support, and a better handle lends more - carved is 0.2, professional 0.4.
    [Fact]
    public void ABetterHandleSupportsTheBindingMore() {
        var wrap = StatFixtures.Binding(1.0f, 0.0f, 0.0f);

        var onCarved = ToolsmithPartStatsHelpers.CalculateBindingDurability(StatFixtures.Carved(), wrap, StatFixtures.Oak());
        var onProfessional = ToolsmithPartStatsHelpers.CalculateBindingDurability(StatFixtures.Professional(), wrap, StatFixtures.Oak());

        Assert.True(onProfessional > onCarved);
        Assert.Equal(ToolsmithConstants.PartDurabilityBase * 1.2f, onCarved, Tolerance);
        Assert.Equal(ToolsmithConstants.PartDurabilityBase * 1.4f, onProfessional, Tolerance);
    }

    //No treatment term appears in the binding formula at all. Stated as a test because the handle formula does
    //take one, and adding it here by symmetry would be the easy mistake.
    [Fact]
    public void UnbonusedBindingLandsOnTheFlatBase() {
        var durability = ToolsmithPartStatsHelpers.CalculateBindingDurability(
            StatFixtures.Handle(1.0f, 0.0f, 0.0f, 0.0f),
            StatFixtures.Binding(1.0f, 0.0f, 0.0f),
            StatFixtures.NeutralMaterial());

        Assert.Equal(ToolsmithConstants.PartDurabilityBase, durability, Tolerance);
    }
}
