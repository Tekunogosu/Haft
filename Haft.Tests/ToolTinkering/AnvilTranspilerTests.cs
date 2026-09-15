using System.Reflection;
using System.Reflection.Emit;
using Haft.Tests.Support;
using Haft.ToolTinkering;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Haft.Tests.ToolTinkering;

//The transpiler that stamps a smithed handle at the moment it is made. It rewrites a game method by matching IL,
//which is the one thing in this mod that a game update can break silently: the patch still loads, the injection
//just never lands, and handles quietly come out unstamped. These tests run the real transpiler over the real
//method's instructions, so a mismatch after an update fails here rather than in a world.
[Collection("ModStatics")]
public class AnvilTranspilerTests {

    private static List<CodeInstruction> RealInstructions() {
        var method = AccessTools.Method(typeof(BlockEntityAnvil), "CheckIfFinished");
        Assert.NotNull(method);
        return PatchProcessor.GetCurrentInstructions(method).ToList();
    }

    //The anchor the transpiler matches on: the clone of the recipe output, followed by the store into its local.
    //If the game ever stops cloning into a local here, this is the test that says so.
    [Fact]
    public void TheMethodStillClonesTheOutputIntoALocal() {
        var codes = RealInstructions();
        var cloneMethod = AccessTools.Method(typeof(ItemStack), nameof(ItemStack.Clone));

        var matches = codes.Where((code, i) =>
            code.opcode == OpCodes.Callvirt
            && ReferenceEquals(code.operand, cloneMethod)
            && i + 1 < codes.Count
            && codes[i + 1].IsStloc()).ToList();

        Assert.Single(matches);
    }

    //The injection itself: three instructions - load the stack, load the anvil, call the hook - added just after
    //the store. Counting them rather than comparing whole listings keeps the test readable when the method around
    //the anchor changes, which it may do without affecting the patch.
    [Fact]
    public void TheTranspilerInjectsTheStampCall() {
        var original = RealInstructions();
        var patched = AnvilSmithedHandlePatches.CheckIfFinishedTranspiler(original).ToList();

        Assert.Equal(original.Count + 3, patched.Count);

        var hook = AccessTools.Method(typeof(AnvilSmithedHandlePatches),
            nameof(AnvilSmithedHandlePatches.StampSmithedHandle));
        var callIndex = patched.FindIndex(code => code.opcode == OpCodes.Call && ReferenceEquals(code.operand, hook));
        Assert.True(callIndex > 0, "The transpiler did not inject a call to the stamping hook.");

        //The two instructions before the call must be what the hook is declared to take, in order: the finished
        //stack read back out of its local, then the anvil itself.
        Assert.True(patched[callIndex - 2].IsLdloc(), "The finished stack is not loaded before the hook is called.");
        Assert.Equal(OpCodes.Ldarg_0, patched[callIndex - 1].opcode);
    }

    //The call has to land after the store, not before it - injected ahead of the store the local would still be
    //empty and the hook would stamp nothing.
    [Fact]
    public void TheStampCallLandsAfterTheStackIsStored() {
        var patched = AnvilSmithedHandlePatches.CheckIfFinishedTranspiler(RealInstructions()).ToList();

        var cloneMethod = AccessTools.Method(typeof(ItemStack), nameof(ItemStack.Clone));
        var hook = AccessTools.Method(typeof(AnvilSmithedHandlePatches),
            nameof(AnvilSmithedHandlePatches.StampSmithedHandle));

        var cloneIndex = patched.FindIndex(code => code.opcode == OpCodes.Callvirt && ReferenceEquals(code.operand, cloneMethod));
        var callIndex = patched.FindIndex(code => code.opcode == OpCodes.Call && ReferenceEquals(code.operand, hook));

        Assert.True(patched[cloneIndex + 1].IsStloc(), "The clone is no longer stored straight into a local.");
        Assert.Equal(cloneIndex + 2, callIndex - 2);
    }

    //A method with no anchor must come back untouched rather than part-patched, since the postfix fallback is what
    //covers that case. This is the shape the transpiler takes after a game update that moves the clone, and it has
    //to say so in the log: the fallback it drops to is a guess, so a silent miss is the thing to prevent.
    [Fact]
    public void AMethodWithoutTheAnchorIsLeftAloneAndReported() {
        using var statics = new ModStaticsScope();

        var unrelated = new List<CodeInstruction> {
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ret)
        };

        var result = AnvilSmithedHandlePatches.CheckIfFinishedTranspiler(unrelated).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(OpCodes.Ldarg_0, result[0].opcode);
        Assert.Equal(OpCodes.Ret, result[1].opcode);
        Assert.True(statics.Log.ErrorMentions("could not find where the finished stack is stored"),
            "A transpiler that cannot find its anchor has to report it, or the fallback runs silently.");
    }
}
