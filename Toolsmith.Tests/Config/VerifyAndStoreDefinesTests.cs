using Toolsmith.Config;
using Toolsmith.Tests.Support;

namespace Toolsmith.Tests.Config;

//Loading stat and part defines out of the JSON configs. This is the path other mods' compat patches feed into,
//so its job is to survive a malformed entry: drop what cannot be used, say why, and keep loading the rest.
[Collection("ModStatics")]
public class VerifyAndStoreDefinesTests {

    private static HandleStatDefines HandleStat(string id) =>
        new() { id = id, baseHPfactor = 1.0f, selfHPBonus = 0.0f, bindingHPBonus = 0.0f, speedBonus = 0.0f };

    [Fact]
    public void StoresEachDefineUnderItsOwnId() {
        using var scope = new ModStaticsScope();
        var target = new Dictionary<string, HandleStatDefines>();

        ToolsmithPartStatsHelpers.VerifyAndStoreDefinesInDict(
            new List<HandleStatDefines> { HandleStat("stick"), HandleStat("professional") }, false, ref target);

        Assert.Equal(2, target.Count);
        Assert.True(target.ContainsKey("stick"));
        Assert.True(target.ContainsKey("professional"));
    }

    //An entry with no id cannot be keyed, so it is skipped rather than throwing - one bad compat patch must not
    //stop every other define from loading.
    [Fact]
    public void SkipsAnEntryWithNoIdAndKeepsLoadingTheRest() {
        using var scope = new ModStaticsScope();
        var target = new Dictionary<string, HandleStatDefines>();

        ToolsmithPartStatsHelpers.VerifyAndStoreDefinesInDict(
            new List<HandleStatDefines> { HandleStat(null), HandleStat("professional") }, false, ref target);

        Assert.Single(target);
        Assert.True(target.ContainsKey("professional"));
        Assert.True(scope.Log.ErrorMentions("lacks an id"));
    }

    //TESTING.md section 8: a duplicate id produced "Attempted to add a MaterialStatDefine that already exists"
    //at startup. The first entry wins and the second is reported.
    [Fact]
    public void ReportsADuplicateIdAndKeepsTheFirstEntry() {
        using var scope = new ModStaticsScope(enableEdits: false);
        var target = new Dictionary<string, HandleStatDefines>();
        var first = HandleStat("metal");
        var second = HandleStat("metal");
        second.baseHPfactor = 99.0f;

        ToolsmithPartStatsHelpers.VerifyAndStoreDefinesInDict(
            new List<HandleStatDefines> { first, second }, false, ref target);

        Assert.Single(target);
        Assert.Equal(1.0f, target["metal"].baseHPfactor);
        Assert.True(scope.Log.ErrorMentions("already exists"));
    }

    //With edits enabled a second entry for the same id is the intended way to retune a define, so it must not
    //be reported as an error. The first entry still wins - this only silences the message.
    [Fact]
    public void DoesNotReportADuplicateWhenEditsAreEnabled() {
        using var scope = new ModStaticsScope(enableEdits: true);
        var target = new Dictionary<string, HandleStatDefines>();

        ToolsmithPartStatsHelpers.VerifyAndStoreDefinesInDict(
            new List<HandleStatDefines> { HandleStat("metal"), HandleStat("metal") }, false, ref target);

        Assert.Single(target);
        Assert.False(scope.Log.ErrorMentions("already exists"));
    }

    //A part define is useless without its stat tag - it would have nothing to look its numbers up under - so
    //that one is dropped rather than defaulted.
    [Fact]
    public void DropsAPartDefineWithNoStatTag() {
        using var scope = new ModStaticsScope();
        var target = new Dictionary<string, HandlePartDefines>();

        ToolsmithPartStatsHelpers.VerifyAndStoreDefinesInDict(
            new List<HandlePartDefines> {
                new() { id = "tagless", handleStatTag = null },
                new() { id = "good", handleStatTag = "handle" }
            }, false, ref target);

        Assert.Single(target);
        Assert.True(target.ContainsKey("good"));
        Assert.True(scope.Log.ErrorMentions("stat tag"));
    }

    //An unset numeric field defaults instead: the stats come out wrong but the part still works, which is more
    //useful than refusing to load it. Only checked when the config asks for the full check.
    [Fact]
    public void DefaultsAnUnsetNumericFieldUnderTheFullCheck() {
        using var scope = new ModStaticsScope();
        var target = new Dictionary<string, HandleStatDefines>();
        var incomplete = new HandleStatDefines { id = "half", baseHPfactor = -1.0f, selfHPBonus = -1.0f,
                                                 bindingHPBonus = -1.0f, speedBonus = -1.0f };

        ToolsmithPartStatsHelpers.VerifyAndStoreDefinesInDict(
            new List<HandleStatDefines> { incomplete }, true, ref target);

        Assert.Equal(1.0f, target["half"].baseHPfactor);
        Assert.Equal(0.0f, target["half"].selfHPBonus);
        Assert.True(scope.Log.ErrorMentions("BaseHPFactor"));
    }

    //The full check is dev-time verification rather than a load step, so an unset field passes through untouched
    //when it is off. Worth stating because the defaulted and undefaulted values differ.
    [Fact]
    public void LeavesAnUnsetFieldAloneWithoutTheFullCheck() {
        using var scope = new ModStaticsScope();
        var target = new Dictionary<string, HandleStatDefines>();

        ToolsmithPartStatsHelpers.VerifyAndStoreDefinesInDict(
            new List<HandleStatDefines> { new() { id = "half", baseHPfactor = -1.0f } }, false, ref target);

        Assert.Equal(-1.0f, target["half"].baseHPfactor);
    }

    //Older configs and the compat mods written against them spell densityFactor as hardnessFactor. It is folded
    //in before the completeness check, so such an entry loads as complete rather than being defaulted to 1.0.
    [Fact]
    public void FoldsTheLegacyHardnessFactorIntoDensityFactor() {
        using var scope = new ModStaticsScope();
        var target = new Dictionary<string, MaterialStatDefines>();

        ToolsmithPartStatsHelpers.VerifyAndStoreDefinesInDict(
            new List<MaterialStatDefines> {
                new() { id = "legacyoak", hardnessFactor = 2.5f, densityFactor = -1.0f, nailBindingBonus = 0.0f }
            }, true, ref target);

        Assert.Equal(2.5f, target["legacyoak"].densityFactor);
        Assert.False(scope.Log.ErrorMentions("DensityFactor"));
    }

    //A define spelling both takes the modern one, so a config being migrated does not silently revert.
    [Fact]
    public void PrefersDensityFactorWhenBothAreSpelled() {
        using var scope = new ModStaticsScope();
        var target = new Dictionary<string, MaterialStatDefines>();

        ToolsmithPartStatsHelpers.VerifyAndStoreDefinesInDict(
            new List<MaterialStatDefines> {
                new() { id = "oak", densityFactor = 1.0f, hardnessFactor = 9.0f, nailBindingBonus = 0.0f }
            }, true, ref target);

        Assert.Equal(1.0f, target["oak"].densityFactor);
    }

    //A metal binding with no metalType cannot say what bits to return when it breaks, so it is dropped rather
    //than stored with a hole in it.
    [Fact]
    public void DropsAMetalBindingWithNoMetalType() {
        using var scope = new ModStaticsScope();
        var target = new Dictionary<string, BindingStatDefines>();

        ToolsmithPartStatsHelpers.VerifyAndStoreDefinesInDict(
            new List<BindingStatDefines> {
                new() { id = "nails", isMetal = true, metalType = null, baseHPfactor = 1.0f },
                new() { id = "twine", isMetal = false, baseHPfactor = 1.0f }
            }, false, ref target);

        Assert.Single(target);
        Assert.True(target.ContainsKey("twine"));
        Assert.True(scope.Log.ErrorMentions("metalType"));
    }

    //A metal binding that does name its metal is the normal case and must load.
    [Fact]
    public void KeepsAMetalBindingThatNamesItsMetal() {
        using var scope = new ModStaticsScope();
        var target = new Dictionary<string, BindingStatDefines>();

        ToolsmithPartStatsHelpers.VerifyAndStoreDefinesInDict(
            new List<BindingStatDefines> {
                new() { id = "nails", isMetal = true, metalType = "iron", baseHPfactor = 1.0f }
            }, false, ref target);

        Assert.True(target.ContainsKey("nails"));
        Assert.False(scope.Log.ErrorMentions("metalType"));
    }

    //An empty config is not an error - a compat file for a mod that is not installed loads as nothing.
    [Fact]
    public void AnEmptyListLoadsNothingAndReportsNothing() {
        using var scope = new ModStaticsScope();
        var target = new Dictionary<string, HandleStatDefines>();

        ToolsmithPartStatsHelpers.VerifyAndStoreDefinesInDict(new List<HandleStatDefines>(), true, ref target);

        Assert.Empty(target);
        Assert.Empty(scope.Log.Errors);
    }
}
