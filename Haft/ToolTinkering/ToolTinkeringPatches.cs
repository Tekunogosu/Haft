using Cairo;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using Haft.Client;
using Haft.Client.Behaviors;
using Haft.ToolTinkering.Behaviors;
using Haft.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Haft.ToolTinkering {

    [HarmonyPatch(typeof(CollectibleObject))]
    [HarmonyPatchCategory(HaftModSystem.ToolTinkeringTransitionalPropsPatchCategory)]
    public class ToolTinkeringTransitionalPropsPatches {

        [HarmonyPostfix]
        //Replaces the result outright rather than appending: a part carrying a wet treatment has one transition, the
        //drying of that treatment, and no vanilla transition to preserve alongside it.
        [HarmonyPatch(nameof(CollectibleObject.GetTransitionableProperties))]
        private static void ToolPartTransitionalOverridePostfix(ref TransitionableProperties[] __result, IWorldAccessor world, ItemStack itemstack, Entity forEntity) {
            if (itemstack.Collectible.HasBehavior<ModularPartRenderingFromAttributes>()) {
                if (itemstack.HasWetTreatment()) {
                    var itemCopy = itemstack.Clone();
                    itemCopy.RemoveWetTreatment();
                    var transProp = new TransitionableProperties {
                        Type = EnumTransitionType.Dry,
                        FreshHours = NatFloat.createUniform(0, 0),
                        TransitionHours = NatFloat.createUniform(itemstack.GetWetTreatment(), 0f),
                        TransitionedStack = new JsonItemStack { ResolvedItemstack = itemCopy },
                        TransitionRatio = 1
                    };

                    __result = new TransitionableProperties[] { transProp };
                }
            }
        }
    }

    [HarmonyPatch(typeof(CollectibleObject))]
    [HarmonyPatchCategory(HaftModSystem.ToolTinkeringCraftingPatchCategory)]
    public class ToolTinkeringCraftingPatches {

        //Stamping the adhesive has to happen here as well as in ConsumeCraftingIngredients. OnCreatedByCrafting is
        //what fills the output slot the player is looking at BEFORE they take the craft, so without this the preview
        //shows a plain grip and only the taken item says it is backed. ConsumeCraftingIngredients still does it too,
        //because that is the call that survives paths which never build a preview.
        [HarmonyPostfix]
        [HarmonyPatch(nameof(CollectibleObject.OnCreatedByCrafting))]
        private static void OnCreatedByCraftingAdhesiveGripPostfix(ItemSlot[] allInputSlots, ItemSlot outputSlot, IRecipeBase byRecipe) {
            StampAdhesiveOnBackedGrip(allInputSlots, outputSlot, byRecipe as GridRecipe);
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(CollectibleObject.ConsumeCraftingIngredients))]
        private static bool ConsumeCraftingIngredientsModularPartPrefix(ItemSlot[] slots, ItemSlot outputSlot, GridRecipe matchingRecipe, ref bool __result) {
            //Backing a grip with an adhesive produces the same grip carrying an attribute, so there is no new item
            //whose behaviour could catch this. Grips have no behaviour of their own either - attaching one to roughly
            //130 items to serve a single recipe group would cost far more than reading the recipe here.
            StampAdhesiveOnBackedGrip(slots, outputSlot, matchingRecipe);

            if (outputSlot.Itemstack.HasDisposeMeNowPlease()) {
                outputSlot.Itemstack = null;
                outputSlot.MarkDirty();

                __result = true;
                return false;
            }

            return true;
        }

        //A backed grip is the same item as a plain one, so without a line saying so the two are indistinguishable in
        //an inventory. Grips carry no behaviour of their own to hang this on, hence the patch.
        [HarmonyPostfix]
        [HarmonyPatch(nameof(CollectibleObject.GetHeldItemInfo))]
        private static void GetHeldItemInfoAdhesiveGripPostfix(ItemSlot inSlot, StringBuilder dsc) {
            var stack = inSlot?.Itemstack;
            if (stack == null || !stack.HasGripAdhesiveTag()) {
                return;
            }

            if (string.IsNullOrEmpty(stack.GetGripAdhesiveTag())) {
                return;
            }

            dsc.AppendLine(Lang.Get("gripadhesivebacked"));
        }

        private static void StampAdhesiveOnBackedGrip(ItemSlot[] slots, ItemSlot outputSlot, GridRecipe matchingRecipe) {
            if (slots == null || matchingRecipe?.RecipeGroup != HaftConstants.AdhesiveGripRecipeGroup) {
                return;
            }

            var output = outputSlot?.Itemstack;
            if (output?.Collectible?.Code == null || !HaftModSystem.Stats.GripParts.ContainsKey(output.Collectible.Code.Path)) {
                return;
            }

            //Called from both the preview and the consume path, so the second call has nothing left to do.
            if (output.HasGripAdhesiveTag()) {
                return;
            }

            //Which adhesive was used is worth keeping rather than a bare flag: it is what a tint would key off, and
            //what a later mechanic would read to tell hide glue from pitch.
            foreach (var slot in slots) {
                var stack = slot?.Itemstack;
                if (stack?.Collectible == null) {
                    continue;
                }

                var content = TinkeringUtility.GetBindingContent(stack);
                if (content?.Collectible?.Code == null) {
                    continue;
                }

                var bindingPart = HaftModSystem.Stats.BindingParts.TryGetValue(content.Collectible.Code.Path);
                if (bindingPart != null && bindingPart.bindingStatTag == HaftConstants.AdhesiveBindingStatTag) {
                    output.SetGripAdhesiveTag(content.Collectible.Code.Path);
                    return;
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(CollectibleObject.Equals))]
        private static bool EqualsModularPartPrefix(ItemStack thisStack, ItemStack otherStack, ref bool __result, params string[] ignoreAttributeSubTrees) {
            if (ignoreAttributeSubTrees != null && thisStack.Collectible.HasBehavior<ModularPartRenderingFromAttributes>() && thisStack.Collectible.MaxStackSize > 1) {
                if (thisStack.Class == otherStack.Class && thisStack.Id == otherStack.Id) {
                    var newIgnoreAttributes = ignoreAttributeSubTrees.Remove(HaftAttributes.ModularMultiPartDataTree).Remove(HaftAttributes.ModularPartDataTree);
                    __result = thisStack.Attributes.Equals(HaftModSystem.Api.World, otherStack.Attributes, newIgnoreAttributes);
                    return false;
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(CollectibleObject))]
    [HarmonyPatchCategory(HaftModSystem.ToolTinkeringRenderPatchCategory)]
    public class ToolTinkeringRenderPatches {

        [HarmonyPrefix]
        [HarmonyPatch(nameof(CollectibleObject.ShouldDisplayItemDamage))]
        private static bool TinkeredToolShouldDisplayItemDamagePrefix(ItemStack itemstack, ref bool __result) {
            if (itemstack.Collectible.HasBehavior<CollectibleBehaviorTinkeredTools>() || TinkeringUtility.IsValidHead(itemstack) || TinkeringUtility.IsValidHandle(itemstack)) {
                var lowestCurrent = TinkeringUtility.FindLowestCurrentDurabilityForBar(itemstack);
                var lowestMax = TinkeringUtility.FindLowestMaxDurabilityForBar(itemstack);

                __result = (lowestCurrent != lowestMax);

                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(CollectibleObject))]
    [HarmonyPatchCategory(HaftModSystem.OffhandDominantInteractionUsePatchCategory)]
    public class OffhandDominantInteractionUsePatches {

        //An item in the offhand can claim an interaction the main hand would otherwise handle - a whetstone honing
        //the held tool, rather than the tool being used on the world. All four patches ask the same question first:
        //is there such an item in the offhand, and does it want this particular interaction. Null when the answer is
        //no, in which case the patch returns true and vanilla proceeds untouched.
        private static CollectibleBehaviorOffhandDominantInteraction ClaimingOffhandBehavior(EntityAgent byEntity, ItemSlot slot, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent = false) {
            if (byEntity == null || byEntity.LeftHandItemSlot?.Empty != false) {
                return null;
            }

            var offhand = byEntity.LeftHandItemSlot.Itemstack.Collectible;
            if (!offhand.HasBehavior<CollectibleBehaviorOffhandDominantInteraction>()) {
                return null;
            }

            var bh = offhand.GetBehavior<CollectibleBehaviorOffhandDominantInteraction>();
            if (bh.AskItemForHasInteractionAvailable(byEntity.LeftHandItemSlot, slot, byEntity, blockSel, entitySel, firstEvent)) {
                return null;
            }

            return bh;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(CollectibleObject.OnHeldUseStart))]
        private static bool OnHeldUseStartDominantOffhandInteractionPrefix(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumHandInteract useType, bool firstEvent, ref EnumHandHandling handling) {
            if (useType != EnumHandInteract.HeldItemInteract) {
                return true;
            }

            var bh = ClaimingOffhandBehavior(byEntity, slot, blockSel, entitySel, firstEvent);
            if (bh == null) {
                return true;
            }

            bh.OnHeldOffhandDominantStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(CollectibleObject.OnHeldUseStep))]
        private static bool OnHeldUseStepDominantOffhandInteractionPrefix(ref EnumHandInteract __result, float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel) {
            EnumHandInteract handUse = byEntity?.Controls.HandUse ?? EnumHandInteract.None;
            if (handUse == EnumHandInteract.HeldItemAttack) {
                return true;
            }

            var bh = ClaimingOffhandBehavior(byEntity, slot, blockSel, entitySel);
            if (bh == null) {
                return true;
            }

            __result = bh.OnHeldOffhandDominantStep(secondsPassed, slot, byEntity, blockSel, entitySel) ? handUse : EnumHandInteract.None;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(CollectibleObject.OnHeldUseCancel))]
        private static bool OnHeldUseCancelDominantOffhandInteractionPrefix(ref EnumHandInteract __result, float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason) {
            EnumHandInteract handUse = byEntity?.Controls.HandUse ?? EnumHandInteract.None;
            if (handUse == EnumHandInteract.HeldItemAttack) {
                return true;
            }

            var bh = ClaimingOffhandBehavior(byEntity, slot, blockSel, entitySel);
            if (bh == null) {
                return true;
            }

            //Cancel reads the opposite way round to Step: a behavior that handled the cancel ends the interaction.
            __result = bh.OnHeldOffhandDominantCancel(secondsPassed, slot, byEntity, blockSel, entitySel, cancelReason) ? EnumHandInteract.None : handUse;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(CollectibleObject.OnHeldUseStop))]
        private static bool OnHeldUseStopDominantOffhandInteractionPrefix(float secondsPassed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumHandInteract useType) {
            if (useType != EnumHandInteract.HeldItemInteract) {
                return true;
            }

            var bh = ClaimingOffhandBehavior(byEntity, slot, blockSel, entitySel);
            if (bh == null) {
                return true;
            }

            bh.OnHeldOffhandDominantStop(secondsPassed, slot, byEntity, blockSel, entitySel);
            return false;
        }
    }

    //Patching the GuiElementItemSlotGridBase now! Anything patching CollectibleObject is above!
    [HarmonyPatch(typeof(GuiElementItemSlotGridBase))]
    [HarmonyPatchCategory(HaftModSystem.ToolTinkeringGuiElementPatchCategory)]
    public class ToolTinkeringGuiElementPatches {

        [HarmonyTranspiler]
        [HarmonyPatch("ComposeSlotOverlays")]
        private static IEnumerable<CodeInstruction> ComposeSlotOverlaysTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator) { //WOW Transpilers are FUN. And actually I do understand them better now having written this.

            int retCount = 0;
            int shadePathCount = 0;
            int index = -1;
            int indexOfSecondRet = -1;
            int indexOfShouldRenderDamageCheck = -1;
            int indexOfDamageColor = -1;
            var targetDamageColor = AccessTools.Method(typeof(CollectibleObject), nameof(CollectibleObject.GetItemDamageColor));
            int indexOfGetMaxDur = -1;
            var targetGetMaxDur = AccessTools.Method(typeof(CollectibleObject), nameof(CollectibleObject.GetMaxDurability));
            int indexOfGetRemainingDur = -1;
            var targetGetRemainingDur = AccessTools.Method(typeof(CollectibleObject), nameof(CollectibleObject.GetRemainingDurability));

            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++) {
                if (retCount < 2 && codes[i].opcode == OpCodes.Ret) { //Don't need to look at anything until after we have found three 'ret' calls
                    retCount++;
                    if (retCount == 2 && codes[i - 1].opcode == OpCodes.Ldc_I4_1) {
                        indexOfSecondRet = i; //Since I don't need to change this now anymore, using it as a stepping stone to find the nearest entry point to the check for if damage should be rendered
                    }
                    continue;
                }

                if (retCount == 2 && indexOfSecondRet > 1 && codes[i].opcode == OpCodes.Brtrue_S) {
                    indexOfShouldRenderDamageCheck = i;
                    indexOfSecondRet = 1;
                    continue;
                }

                if (retCount == 2 && shadePathCount < 2 && codes[i].opcode == OpCodes.Callvirt) {
                    if ((MethodInfo)codes[i].operand == targetDamageColor) {
                        indexOfDamageColor = i;
                        continue;
                    }
                    if ((MethodInfo)codes[i].operand == targetGetMaxDur) {
                        indexOfGetMaxDur = i;
                        continue;
                    }
                    if ((MethodInfo)codes[i].operand == targetGetRemainingDur) {
                        indexOfGetRemainingDur = i;
                        continue;
                    }
                }

                if (retCount == 2 && shadePathCount < 2 && codes[i].opcode == OpCodes.Call) {
                    if (codes[i - 1].opcode == OpCodes.Ldc_R8 && (double)codes[i - 1].operand == (double)(2)) { //If a Call code is preceeded by a float 2 being loaded, it is likely the ShadePath call we are looking for.
                        if (codes[i - 2].opcode == OpCodes.Ldloc_2) { //Preceded by textCtx being loaded, which distinguishes this from other calls taking a literal 2.
                            shadePathCount++;
                            continue;
                        }
                    }
                }

                if (shadePathCount == 2) {
                    index = i;
                    break;
                }
            }

            var codeAddition = new List<CodeInstruction> {
                CodeInstruction.LoadArgument(1),
                CodeInstruction.LoadArgument(2),
                CodeInstruction.LoadArgument(3),
                CodeInstruction.LoadLocal(2),
                CodeInstruction.LoadArgument(0),
                CodeInstruction.Call(typeof(ToolTinkeringGuiElementPatches), "DrawSharpnessBar", new Type[5] { typeof(ItemSlot), typeof(int), typeof(int), typeof(Context), typeof(GuiElementItemSlotGridBase) })
            };

            var haftGetItemDamage = AccessTools.Method(typeof(TinkeringUtility), "HaftGetItemDamageColor", new Type[1] { typeof(ItemStack) });
            var haftGetMaxDur = AccessTools.Method(typeof(TinkeringUtility), "FindLowestMaxDurabilityForBar", new Type[1] { typeof(ItemStack) });
            var haftGetRemainingDur = AccessTools.Method(typeof(TinkeringUtility), "FindLowestCurrentDurabilityForBar", new Type[1] { typeof(ItemStack) });
            var getItemStack = AccessTools.Method(typeof(ItemSlot), "get_Itemstack");
            var haftShouldRenderSharpness = AccessTools.Method(typeof(TinkeringUtility), "ShouldRenderSharpnessBar", new Type[1] { typeof(ItemStack) });

            var shouldRenderSharpnessAddition = new List<CodeInstruction> {
                CodeInstruction.LoadArgument(1),
                new CodeInstruction(OpCodes.Call, getItemStack),
                new CodeInstruction(OpCodes.Call, haftShouldRenderSharpness),
                new CodeInstruction(OpCodes.Brtrue_S, codes[indexOfShouldRenderDamageCheck].operand)
            };

            if (index >= 0 && indexOfSecondRet >= 0 && indexOfShouldRenderDamageCheck >= 0 && indexOfDamageColor >= 0 && indexOfGetMaxDur >= 0 && indexOfGetRemainingDur >= 0) {
                codeAddition[0].MoveLabelsFrom(codes[index]);
                codes.InsertRange(index, codeAddition);
                codes[indexOfDamageColor - 5].opcode = OpCodes.Nop;
                codes[indexOfDamageColor - 4].opcode = OpCodes.Nop;
                codes[indexOfDamageColor - 3].opcode = OpCodes.Nop;
                codes[indexOfDamageColor].opcode = OpCodes.Call;
                codes[indexOfDamageColor].operand = haftGetItemDamage;
                codes[indexOfGetMaxDur - 5].opcode = OpCodes.Nop;
                codes[indexOfGetMaxDur - 4].opcode = OpCodes.Nop;
                codes[indexOfGetMaxDur - 3].opcode = OpCodes.Nop;
                codes[indexOfGetMaxDur].opcode = OpCodes.Call;
                codes[indexOfGetMaxDur].operand = haftGetMaxDur;
                codes[indexOfGetRemainingDur - 5].opcode = OpCodes.Nop;
                codes[indexOfGetRemainingDur - 4].opcode = OpCodes.Nop;
                codes[indexOfGetRemainingDur - 3].opcode = OpCodes.Nop;
                codes[indexOfGetRemainingDur].opcode = OpCodes.Call;
                codes[indexOfGetRemainingDur].operand = haftGetRemainingDur;
                codes.InsertRange(indexOfShouldRenderDamageCheck + 1, shouldRenderSharpnessAddition);
            } else {
                HaftModSystem.Logger.Error("Durability and Sharpness Bar Transpiler had an error!  Will not patch anything, errors will follow:");
                if (index < 0) {
                    HaftModSystem.Logger.Error("Could not find the second call to ShadePath for the Damage Bar rendering.");
                }
                if (indexOfSecondRet < 0) {
                    HaftModSystem.Logger.Error("Could not locate the second return call.");
                }
                if (indexOfShouldRenderDamageCheck < 0) {
                    HaftModSystem.Logger.Error("Could not locate the render damage check.");
                }
                if (indexOfDamageColor < 0) {
                    HaftModSystem.Logger.Error("Could not locate the Damage Color call.");
                }
                if (indexOfGetMaxDur < 0) {
                    HaftModSystem.Logger.Error("Could not locate the GetMaxDurability call.");
                }
                if (indexOfGetRemainingDur < 0) {
                    HaftModSystem.Logger.Error("Could not locate the GetRemainingDurability call.");
                }
            }

            return codes.AsEnumerable();
        }

        //This is basically the vanilla way of handling the Durability bar, but instead I tweaked it to be a little above, and also look at the Sharpness instead of Durability values. ShouldRenderSharpness checks if it's even a tool with sharpness, so this shouldn't run on anything that doesn't actually have it.
        private static void DrawSharpnessBar(ItemSlot slot, int slotId, int slotIndex, Context textCtx, GuiElementItemSlotGridBase instance) {
            //Guard against null slots / empty stacks / out-of-range slot indices. ComposeSlotOverlays runs
            //for every slot in every visible inventory grid (chests, storage vessels, etc.), so a slot
            //containing a non-Haft item is the common case. Without these guards Haft crashes
            //the game on any chest interaction; see issue #35.
            if (slot?.Itemstack == null) {
                return;
            }
            if (instance?.SlotBounds == null || slotIndex < 0 || slotIndex >= instance.SlotBounds.Length) {
                return;
            }
            if (TinkeringUtility.ShouldRenderSharpnessBar(slot.Itemstack)) {
                double x = ElementBounds.scaled(4);
                double y = (int)instance.SlotBounds[slotIndex].InnerHeight - ElementBounds.scaled(8) - ElementBounds.scaled(4);
                textCtx.SetSourceRGBA(GuiStyle.DialogStrongBgColor);
                double width = (instance.SlotBounds[slotIndex].InnerWidth - ElementBounds.scaled(8));
                double height = ElementBounds.scaled(4);
                GuiElement.RoundRectangle(textCtx, x, y, width, height, 1);
                textCtx.FillPreserve();
                instance.ShadePath(textCtx, 2);

                int maxSharp = slot.Itemstack.GetToolMaxSharpness();
                float remainingSharpness = (float)slot.Itemstack.GetToolCurrentSharpness() / maxSharp;
                width = remainingSharpness * (instance.SlotBounds[slotIndex].InnerWidth - ElementBounds.scaled(8));
                float[] color;

                if (HaftModSystem.ClientConfig?.UseGradientForSharpnessInstead == true) {
                    if (TinkeringUtility.GradiantNeedsInit()) {
                        TinkeringUtility.InitializeSharpnessColorGradient();
                    }

                    color = ColorUtil.ToRGBAFloats(TinkeringUtility.GetItemSharpnessColor(slot.Itemstack));
                    textCtx.SetSourceRGB(color[0], color[1], color[2]);

                    GuiElement.RoundRectangle(textCtx, x, y, width, height, 1);
                    textCtx.FillPreserve();
                    instance.ShadePath(textCtx, 2);

                    return;
                }

                if (HaftModSystem.ClientConfig?.ShowAllSharpnessBarSections == true) {
                    double totalBarWidth = (instance.SlotBounds[slotIndex].InnerWidth - ElementBounds.scaled(8));
                    double dx = x;
                    double dWidth;
                    int count = 0;
                    double widthPlotted = 0;

                    while (count < 5) {
                        color = ColorUtil.ToRGBAFloats(TinkeringUtility.GetFlatItemSharpnessColor(count));
                        textCtx.SetSourceRGB(color[0], color[1], color[2]);

                        if (count < 2) {
                            dWidth = 0.15 * totalBarWidth;
                        } else if (count < 3) {
                            dWidth = 0.3 * totalBarWidth;
                        } else {
                            dWidth = 0.2 * totalBarWidth;
                        }

                        if (widthPlotted + dWidth > width) {
                            dWidth = width - widthPlotted;
                        }

                        GuiElement.RoundRectangle(textCtx, dx, y, dWidth, height, 1);
                        textCtx.FillPreserve();
                        instance.ShadePath(textCtx, 2);
                        widthPlotted += dWidth;

                        if (widthPlotted == width) {
                            break;
                        }

                        dx += dWidth;
                        count++;
                    }
                } else {
                    if (remainingSharpness < 0.15) {
                        color = ColorUtil.ToRGBAFloats(TinkeringUtility.GetFlatItemSharpnessColor(0));
                    } else if (remainingSharpness < 0.3) {
                        color = ColorUtil.ToRGBAFloats(TinkeringUtility.GetFlatItemSharpnessColor(1));
                    } else if (remainingSharpness < 0.6) {
                        color = ColorUtil.ToRGBAFloats(TinkeringUtility.GetFlatItemSharpnessColor(2));
                    } else if (remainingSharpness < 0.8) {
                        color = ColorUtil.ToRGBAFloats(TinkeringUtility.GetFlatItemSharpnessColor(3));
                    } else {
                        color = ColorUtil.ToRGBAFloats(TinkeringUtility.GetFlatItemSharpnessColor(4));
                    }
                    textCtx.SetSourceRGB(color[0], color[1], color[2]);

                    GuiElement.RoundRectangle(textCtx, x, y, width, height, 1);
                    textCtx.FillPreserve();
                    instance.ShadePath(textCtx, 2);
                }
            }
        }
    }

    //A thrown tinkered tool is damaged through a DummySlot the projectile wraps around its own ProjectileStack, so
    //nothing Haft does to that slot reaches the projectile. The projectile decides whether to despawn by reading
    //GetRemainingDurability on the stack afterwards - and Haft sets PreventDefault on OnDamageItem, which
    //suppresses the vanilla write that would have decremented it. The value therefore never falls, the projectile
    //never despawns, and every further impact re-runs the break and drops another set of parts.
    //
    //These postfixes close that gap: after the parts have been handed out, a projectile whose tool has ended is told
    //to die, and its stack is cleared first so it cannot be picked back up whole. A held tool that falls apart has
    //its slot emptied for the same reason - this is the projectile's equivalent of that.
    [HarmonyPatch(typeof(EntityProjectileBase))]
    [HarmonyPatchCategory(HaftModSystem.ToolTinkeringProjectilePatchCategory)]
    public class ToolTinkeringProjectileBasePatches {

        //Runs when a projectile hits an entity. DamageProjectile has already called DamageItem, so by now any parts
        //have been dropped at the target's position by HandleBrokenTinkeredTool.
        [HarmonyPostfix]
        [HarmonyPatch("DamageProjectile")]
        private static void DamageProjectilePostfix(EntityProjectileBase __instance) {
            ToolTinkeringProjectileHelper.EndProjectileIfToolIsSpent(__instance, "DamageProjectile");
        }
    }

    //IsColliding is declared on EntityProjectile rather than the base, so it needs its own patch target. This is the
    //block-collision path: a spear that runs out mid-flight and hits the ground rather than a creature.
    [HarmonyPatch(typeof(EntityProjectile))]
    [HarmonyPatchCategory(HaftModSystem.ToolTinkeringProjectilePatchCategory)]
    public class ToolTinkeringProjectilePatches {

        [HarmonyPostfix]
        [HarmonyPatch("IsColliding")]
        private static void IsCollidingPostfix(EntityProjectile __instance) {
            ToolTinkeringProjectileHelper.EndProjectileIfToolIsSpent(__instance, "IsColliding");
        }
    }

    public static class ToolTinkeringProjectileHelper {

        //Both impact paths end the same way, so the decision lives here once rather than in each postfix.
        //
        //"Spent" is any part at or below zero, matching the condition OnDamageItem uses to decide whether to run the
        //break at all. A tool that merely fell apart - head intact, handle gone - is as finished as one whose head
        //broke: the held-tool path empties the slot for both, and the parts have already been handed back either way.
        public static void EndProjectileIfToolIsSpent(EntityProjectileBase projectile, string source) {
            if (projectile?.World == null || !projectile.World.Side.IsServer() || !projectile.Alive) {
                return;
            }

            var stack = projectile.ProjectileStack;
            if (stack?.Collectible == null || !stack.Collectible.HasBehavior<CollectibleBehaviorTinkeredTools>()) {
                return;
            }

            //Read the parts straight off the stack rather than trusting the vanilla durability, which is exactly the
            //value PreventDefault stopped anyone from decrementing.
            var headDur = stack.GetToolheadCurrentDurability();
            var handleDur = stack.GetToolhandleCurrentDurability();
            var bindingDur = stack.GetToolbindingCurrentDurability();
            var spent = (headDur <= 0 || handleDur <= 0 || bindingDur <= 0);

            if (HaftModSystem.Config.DebugMessages) {
                HaftModSystem.Logger.Debug("[BreakTrace] projectile postfix (" + source + ") on " + stack.Collectible.Code + ": head/handle/binding " + headDur + " / " + handleDur + " / " + bindingDur + ", spent: " + spent + ", vanilla durability reads " + stack.Collectible.GetRemainingDurability(stack));
            }

            if (!spent) {
                return;
            }

            //Clear the stack before dying. OnCollected hands ProjectileStack back to whoever picks the projectile up
            //and CanCollect only asks whether it is still alive, so a spear left in place here returns whole on top of
            //the parts already dropped.
            projectile.ProjectileStack = null;
            projectile.Die();

            if (HaftModSystem.Config.DebugMessages) {
                HaftModSystem.Logger.Debug("[BreakTrace] projectile postfix (" + source + "): cleared ProjectileStack and killed the projectile.");
            }
        }
    }

    //Restricts ItemAxe's tree-felling BreakBlock to the server. Running it on both sides leaves the client's copy of
    //felled blocks out of step with the server's - the ghost trees - and consumes client-side RNG that then desyncs.
    [HarmonyPatch(typeof(ItemAxe))]
    [HarmonyPatchCategory(HaftModSystem.ToolTinkeringItemAxePatchCategory)]
    public class ItemAxePatches {

        [HarmonyTranspiler]
        [HarmonyPatch(nameof(ItemAxe.OnBlockBrokenWith))]
        public static IEnumerable<CodeInstruction> OnBlockBrokenWithTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator) {
            var codes = new List<CodeInstruction>(instructions);

            int indexBeforeBreakBlock = -1;
            int indexAfterBreakBlock = -1;
            var breakBlockMethod = AccessTools.Method(typeof(IBlockAccessor), "BreakBlock", new Type[] { typeof(BlockPos), typeof(IPlayer), typeof(float) });

            for (int i = 0; i < codes.Count(); i++) {
                if (codes[i].opcode == OpCodes.Callvirt && (MethodInfo)codes[i].operand == breakBlockMethod) {
                    indexAfterBreakBlock = i + 1;

                    for (int j = i; j > 0; j--) { //Lets roll it back until we find the loading of arg 1, world
                        if (codes[j].opcode == OpCodes.Ldarg_1) {
                            indexBeforeBreakBlock = j;
                            break;
                        }
                    }

                    break;
                }
            }

            if (indexAfterBreakBlock >= 0 && indexBeforeBreakBlock >= 0) {
                var newLabel = ilGenerator.DefineLabel();
                var serverSideBreakBlock = new List<CodeInstruction>() {
                    codes[indexAfterBreakBlock].Clone(),
                    codes[indexAfterBreakBlock+1].Clone(),
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Bne_Un, newLabel)
                };

                codes[indexAfterBreakBlock].labels.Add(newLabel);
                codes.InsertRange(indexBeforeBreakBlock, serverSideBreakBlock);
            } else {
                HaftModSystem.Logger.Error("ItemAxe Transpiler had an error! Will not patch anything, and Ghost Trees will occur. More specifics will follow:");
                if (indexAfterBreakBlock < 0) {
                    HaftModSystem.Logger.Error("Could not find the BreakBlock call.");
                }
                if (indexBeforeBreakBlock < 0) {
                    HaftModSystem.Logger.Error("Could not locate the start of the BreakBlock call. Did something else change it?");
                }
            }

            return codes.AsEnumerable();
        }
    }

    //A smithing recipe never reaches CollectibleBehavior.OnCreatedByCrafting - that hook is for grid crafting - so a
    //handle taken off an anvil would carry no material at all and silently read as oak. The anvil finishing a work
    //item is the only moment where the metal it was worked from is still known, so the material is stamped on here.
    //
    //The metal has to be captured in the PREFIX: by the time CheckIfFinished returns, the work item has been consumed
    //and the output placed, so reading the recipe afterwards finds nothing. The postfix then looks for the finished
    //handle in the player's hands or the anvil's own slot and tags it.
    [HarmonyPatchCategory(HaftModSystem.ToolTinkeringCraftingPatchCategory)]
    public class AnvilSmithedHandlePatches {

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BlockEntityAnvil), "CheckIfFinished")]
        private static void CaptureMetalBeforeFinishing(BlockEntityAnvil __instance, out string __state) {
            __state = null;

            var outputCode = __instance?.SelectedRecipe?.Output?.Code;
            if (outputCode == null || !HaftModSystem.Stats.BaseHandleParts.ContainsKey(outputCode.Path)) {
                return;
            }

            //The metal comes from the work item on the anvil, not from the recipe's ingredient code. The metal handle
            //recipe is declared once against "ingot-*", so its Ingredient.Code stays the unresolved wildcard and
            //EndVariant() on it returns a literal "*" - which is why every handle but iron came out Unknown, iron
            //only reading correctly because it is the fallback. The work item is always concrete: the anvil builds it
            //as workitem-{metal} in TryPlaceOn, so its own variant names the metal actually being worked.
            __state = __instance.WorkItemStack?.Collectible?.GetMetalMaterial();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BlockEntityAnvil), "CheckIfFinished")]
        private static void StampMaterialOnSmithedHandle(BlockEntityAnvil __instance, IPlayer byPlayer, string __state) {
            if (string.IsNullOrEmpty(__state)) {
                return;
            }

            var handSlot = byPlayer?.InventoryManager?.ActiveHotbarSlot;
            if (TryStamp(handSlot, __state)) {
                return;
            }

            //Not in hand - a full hotbar drops the output on the ground instead, and the player picks it up later.
            if (byPlayer?.InventoryManager != null) {
                foreach (var slot in byPlayer.InventoryManager.GetHotbarInventory()) {
                    if (TryStamp(slot, __state)) {
                        return;
                    }
                }
            }
        }

        private static bool TryStamp(ItemSlot slot, string metal) {
            var stack = slot?.Itemstack;
            if (stack?.Collectible?.Code == null) {
                return false;
            }
            if (!HaftModSystem.Stats.BaseHandleParts.ContainsKey(stack.Collectible.Code.Path)) {
                return false;
            }
            if (stack.HasHandleMaterialTag()) {
                return false;
            }

            //Everything the grid path sets on a freshly made handle, because a smithed one has been through none of
            //it: the render tree, the material, the shape and stat tags, and a starting durability.
            ApplyMaterialAndRenderTree(stack, metal);
            stack.SetPartCurrentDurability(HaftConstants.PartDurabilityBase);
            stack.SetPartMaxDurability(HaftConstants.PartDurabilityBase);

            slot.MarkDirty();
            return true;
        }

        //The material tag plus the render tree that makes a handle LOOK like that material. Shared with the recipe
        //selector preview, which needs exactly this and nothing else - a preview stack must not be given durability,
        //since it is a display clone that never becomes a real item.
        internal static void ApplyMaterialAndRenderTree(ItemStack stack, string metal) {
            ITreeAttribute multiPartTree = stack.GetMultiPartRenderTree();
            ITreeAttribute handlePartAndTransformTree = multiPartTree.GetPartAndTransformRenderTree(HaftAttributes.ModularPartHandleName);
            ITreeAttribute handleRenderTree = handlePartAndTransformTree.GetPartRenderTree();
            ITreeAttribute handleTextureTree = handleRenderTree.GetPartTextureTree();

            var texturePath = HaftConstants.HandleMetalTexturePathMinusType + metal;
            if (!HaftModSystem.Api.Assets.Exists(new AssetLocation(texturePath + ".png"))) {
                texturePath = HaftConstants.IngotMetalBackupPathMinusType + metal;
            }
            handleTextureTree.SetPartTexturePathFromKey("wood", texturePath);

            stack.SetHandleMaterialTag(metal);

            var handleStats = HaftModSystem.Stats.BaseHandleParts.TryGetValue(stack.Collectible.Code.Path);
            if (handleStats != null) {
                handleRenderTree.SetPartShapePath(handleStats.handleShapePath);
                stack.SetHandleStatTag(handleStats.handleStatTag);
            }
        }
    }

    //The anvil's recipe selector renders each recipe's Output.ResolvedItemstack directly. That works in vanilla
    //because a tool head is a separate registered item per metal - pickaxehead-copper carries its own texture, so
    //the dialog gets the right colour for free. Haft's metal handle is one item for all 18 metals, with the
    //metal held as a stack attribute and the texture chosen at render time, so the recipe's output stack is blank
    //and every preview falls back to the default texture (iron).
    //
    //The fix goes on the DIALOG rather than on BlockEntityAnvil.OpenDialog, for two reasons found the hard way:
    //OpenDialog copies the stacks into a local list before a postfix could touch them, so the change would never be
    //seen; and Output.ResolvedItemstack is the recipe registry's own shared instance, which CheckIfFinished also
    //reads when producing the real item - writing a stamped stack back there would make every future handle come out
    //as whichever metal was previewed last. Cloning into the dialog's own array avoids both.
    //
    //Cosmetic and client-side only. The real handle is stamped in CheckIfFinished from the work item on the anvil.
    [HarmonyPatchCategory(HaftModSystem.ToolTinkeringCraftingPatchCategory)]
    public class AnvilRecipeSelectorPreviewPatches {

        private static bool loggedPreviewFailure = false;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GuiDialogBlockEntityRecipeSelector), MethodType.Constructor,
            new Type[] { typeof(string), typeof(ItemStack[]), typeof(Action<int>), typeof(Action), typeof(BlockPos), typeof(ICoreClientAPI) })]
        private static void StampMetalOnPreviewStacks(ItemStack[] recipeOutputs, BlockPos blockEntityPos, ICoreClientAPI capi) {
            try {
                if (recipeOutputs == null || capi == null || blockEntityPos == null) {
                    return;
                }

                //Only the anvil's selector should be touched. Any other block opening this same dialog is left alone.
                var anvil = capi.World.BlockAccessor.GetBlockEntity(blockEntityPos) as BlockEntityAnvil;
                var metal = anvil?.WorkItemStack?.Collectible?.GetMetalMaterial();
                if (string.IsNullOrEmpty(metal)) {
                    return;
                }

                for (int i = 0; i < recipeOutputs.Length; i++) {
                    var output = recipeOutputs[i];
                    if (output?.Collectible?.Code == null) {
                        continue;
                    }
                    if (!HaftModSystem.Stats.BaseHandleParts.ContainsKey(output.Collectible.Code.Path)) {
                        continue;
                    }
                    if (output.HasHandleMaterialTag()) {
                        continue;
                    }

                    //Clone before stamping: this array element still points at the recipe registry's shared output
                    //stack, and mutating it would leak this metal into every other anvil and into the finished item.
                    var preview = output.Clone();
                    AnvilSmithedHandlePatches.ApplyMaterialAndRenderTree(preview, metal);
                    recipeOutputs[i] = preview;

                    if (HaftModSystem.Config.DebugMessages) {
                        HaftModSystem.Logger.Debug("Anvil recipe preview [" + i + "]: " + output.Collectible.Code.Path +
                            " -> metal '" + metal + "'" +
                            ", clone is separate instance: " + (!ReferenceEquals(preview, output)) +
                            ", source left unstamped: " + (!output.HasHandleMaterialTag()) +
                            ", preview tag: " + (preview.GetHandleMaterialTag() ?? "null"));
                    }
                }
            } catch (Exception e) {
                //A broken preview must never stop the anvil from working - the dialog still opens and the handles
                //just show the default texture, exactly as they did before this patch existed.
                if (!loggedPreviewFailure) {
                    loggedPreviewFailure = true;
                    HaftModSystem.Logger.Warning("Could not stamp the metal onto the anvil recipe selector previews, so they will show the default texture. Cosmetic only. Reason: " + e.Message);
                }
            }
        }
    }

    //Charcoal bluing, as a forge interaction rather than a rub-on treatment. Charcoal is the packing medium heated
    //around the part, not something smeared onto it, so it cannot be a treatment part the way grease or vinegar are.
    //
    //The process only cares about reaching roughly 300C and does not care how the part cools: the black oxide layer
    //forms while hot, and traditionally the piece is simply left in air afterwards. So this watches for a handle in a
    //forge getting hot enough and marks it blued in place - the same item comes out, now treated.
    [HarmonyPatchCategory(HaftModSystem.ToolTinkeringCraftingPatchCategory)]
    public class ForgeBluingPatches {

        //BlockEntityForge exposes everything this needs as public API: WorkItemStack, FuelLevel and IsBurning. An
        //earlier version read private fields named contents, fuelLevel and burning reflectively; none of the three
        //exist. The stack lives in an InventoryGeneric, and "contents" is only a tree-attribute key in
        //FromTreeAttributes, not a field. Reflection here bought nothing and hid the mistake until load time.

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BlockEntityForge), "OnCommonTick200ms")]
        private static void BlueHandleAtTemperature(BlockEntityForge __instance) {
            //OnCommonTick200ms is the forge's own tick, registered on both sides every 200ms. There is no OnGameTick
            //on BlockEntityForge - naming one made Harmony throw at startup, which took the whole mod down with it.
            //Bluing is an authoritative state change, so it is left to the server.
            if (__instance?.Api == null || __instance.Api.Side != EnumAppSide.Server) {
                return;
            }

            var stack = __instance.WorkItemStack;
            if (stack?.Collectible?.Code == null) {
                return;
            }

            //Only a treatable handle, and only one not already carrying a treatment.
            var part = HaftModSystem.Stats.BaseHandleParts.Get(stack.Collectible.Code.Path);
            if (part == null || !part.canBeTreated) {
                return;
            }
            if (stack.HasHandleTreatmentTag()) {
                return;
            }

            //Bluing is a metal finish. The tag check is what keeps it off a wooden handle, which would simply burn.
            if (!ConfigUtility.TagsSatisfy(new string[] { "metal" }, stack.GetHandleProvidedTags())) {
                return;
            }

            //The forge must actually be burning charcoal around the piece. Without this, any handle that happened to
            //be hot would blue the instant it touched a forge - including one just off the anvil, which passes through
            //300C on its way down from working heat. Requiring live fuel is what makes bluing a thing the player
            //chooses to do rather than something that happens to them.
            if (!__instance.IsBurning || __instance.FuelLevel <= 0f) {
                return;
            }

            var temperature = stack.Collectible.GetTemperature(__instance.Api.World, stack);

            //A handle has to be COOLED before it can be blued, then deliberately brought back up. Without this, a
            //piece taken straight off the anvil and dropped in a lit forge blues on its way down from working heat -
            //the player never performed the process, they just failed to wait.
            //
            //Arming happens here rather than on a tick of the handle itself, because a handle spends almost all its
            //life outside a forge and ticking every one of them to watch a temperature nothing else reads would cost
            //far more than it is worth. Cooling below the mark ANYWHERE is what matters, and the forge is the only
            //place the answer is ever needed, so it is checked on arrival and each tick thereafter.
            if (temperature < HaftConstants.BluingCooledTemperature) {
                if (!stack.IsReadyToBlue()) {
                    stack.SetReadyToBlue();
                    __instance.MarkDirty(true);
                }
                return;
            }

            //Still hot and never recorded as cooled: this is a piece that came straight from the anvil, so it heats
            //without blueing. Leaving it in until the fire dies, or pulling it out to cool, arms it for a second pass.
            if (temperature < HaftConstants.BluingTemperature || !stack.IsReadyToBlue()) {
                return;
            }

            stack.SetHandleTreatmentTag(HaftConstants.BluingTreatmentTag);
            __instance.MarkDirty(true);
        }
    }
}
