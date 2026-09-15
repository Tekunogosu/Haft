using HarmonyLib;
using Vintagestory.GameContent;

namespace Haft.Tests.ToolTinkering;

public class MatchesRecipeResolutionTests {
    //The prefix calls MatchesRecipe by reflection. If the name or signature ever changes, AccessTools returns null
    //and the guard reads as "not finished", which silently stops stamping every smithed handle. This is the test
    //that says so at build time rather than in a world.
    [Fact]
    public void MatchesRecipeStillResolves() {
        var method = AccessTools.Method(typeof(BlockEntityAnvil), "MatchesRecipe");
        Assert.NotNull(method);
        Assert.Equal(typeof(bool), method.ReturnType);
        Assert.Empty(method.GetParameters());
    }
}
