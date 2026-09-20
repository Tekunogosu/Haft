using System;
using System.Linq;
using System.Text;
using Haft.Compat;
using Haft.Config;
using Haft.Utils;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Haft.ToolTinkering.Items;
using Haft.ToolTinkering.Behaviors;
using Haft.Client;
using Vintagestory.GameContent;
using Vintagestory.API.Config;
using Haft.ToolTinkering.Drawbacks;
using Vintagestory.API.Datastructures;
using Newtonsoft.Json.Linq;

namespace Haft.ToolTinkering {
    //The shared operations of the tinkering system: identifying parts, crafting and breaking tools, and the three
    //steps of honing. Anything a tool, a part, a block or a patch all need to agree on lives here rather than being
    //answered separately in each of them.
    //
    //Split across several files by responsibility, as one partial class so that every existing call site is
    //unaffected. This file holds what a tool's parts ARE and what happens when one breaks; see
    //TinkeringUtility.Bars.cs, .Crafting.cs and .Sharpening.cs for the rest.
    public static partial class TinkeringUtility {
        public static bool HandleBrokenTinkeredTool(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, int remainingHeadDur, int remainingSharpness, int remainingHandleDur, int remainingBindingDur, bool headBroke, bool refillSlot) {
            ItemStack brokenToolStack = itemslot.Itemstack;
            CollectibleObject toolObject = brokenToolStack.Collectible;
            ItemStack toolHead = null;
            bool gaveHead = false;
            ItemStack toolHandle = null;
            bool gaveHandle = false;
            ItemStack toolBinding = brokenToolStack.GetToolbinding(); //This one is the only different one since it COULD be null representing a lack of a binding, but need to see if it even has a binding first by checking this fact
            bool gaveBinding = false;
            ItemStack bitsDrop = null;

            //A head that is only the placeholder Candle is not a head that broke: it is a tool whose head was
            //never recorded. Both end the tool, so both leave the stack for the caller to destroy, but only a
            //head that actually broke has a head worth accounting for.
            bool headIsPlaceholder = brokenToolStack.HasPlaceholderHead();

            //A tool damaged through a slot with no inventory is not in anyone's hands - a thrown spear is damaged
            //through a DummySlot the projectile wraps around its own ProjectileStack. The projectile decides whether
            //to despawn by reading GetRemainingDurability on that same stack afterwards, so the value logged here is
            //what it will see, and the one logged on exit is what it actually gets.
            if (HaftModSystem.Config.DebugMessages && world.Api.Side.IsServer()) {
                HaftModSystem.Logger.Debug("[BreakTrace] --- HandleBrokenTinkeredTool entry for " + toolObject.Code + " ---");
                HaftModSystem.Logger.Debug("[BreakTrace] slot is " + itemslot.GetType().Name + ", inventory is " + (itemslot.Inventory == null ? "NULL (not held - projectile or dummy)" : itemslot.Inventory.GetType().Name));
                HaftModSystem.Logger.Debug("[BreakTrace] byEntity is " + (byEntity == null ? "null" : byEntity.GetType().Name + " (" + byEntity.Code + ")"));
                HaftModSystem.Logger.Debug("[BreakTrace] incoming head/handle/binding durability: " + remainingHeadDur + " / " + remainingHandleDur + " / " + remainingBindingDur);
                HaftModSystem.Logger.Debug("[BreakTrace] incoming headBroke: " + headBroke + ", refillSlot: " + refillSlot + ", headIsPlaceholder: " + headIsPlaceholder);
                HaftModSystem.Logger.Debug("[BreakTrace] vanilla durability on the stack right now: " + toolObject.GetRemainingDurability(brokenToolStack) + " (this is what a projectile reads to decide whether to despawn)");
            }

            toolHead = brokenToolStack.GetToolhead();
            if (remainingHeadDur > 0 && !headIsPlaceholder) {
                toolHead.SetPartCurrentDurability(remainingHeadDur);
                toolHead.SetPartMaxDurability(brokenToolStack.GetToolheadMaxDurability());
                toolHead.SetPartCurrentSharpness(remainingSharpness);
                toolHead.SetPartMaxSharpness(brokenToolStack.GetToolMaxSharpness());
                if (brokenToolStack.HasTotalHoneValue()) {
                    toolHead.SetTotalHoneValue(brokenToolStack.GetTotalHoneValue());
                }
            } else if (remainingHeadDur <= 0) {
                //The head is what makes a tool a tool: only a broken head is a break in the vanilla sense, where
                //other mods' on-damage handling should run. A handle or binding giving out instead leaves the tool
                //"fallen apart" - its surviving parts are handed back here and vanilla is never told it broke.
                headBroke = true;
            }

            if (HaftModSystem.Api.ModLoader.IsModEnabled("canjewelry")) {
                CanJewelryCompat.CheckAndHandleJewelryStatTransfer(brokenToolStack, toolHead);
            }

            if (remainingHandleDur > 0) {
                var handleToCheck = brokenToolStack.GetToolhandle();
                handleToCheck.SetPartCurrentDurability(remainingHandleDur);
                handleToCheck.SetPartMaxDurability(brokenToolStack.GetToolhandleMaxDurability());
                var handlePercentDamage = handleToCheck.GetPartRemainingHPPercent();
                float comparedPercent = 0.0f;
                if (handleToCheck.Collectible.Code != HaftConstants.DefaultHandleCode && handleToCheck.Collectible.Code != HaftConstants.BoneHandleCode) { //Is this handle not a stick or bone?
                    comparedPercent = HaftConstants.OtherHandleFailurePercent;
                } else {
                    comparedPercent = HaftConstants.StickAndBoneFailurePercent;
                }

                if (handlePercentDamage > comparedPercent) {
                    toolHandle = handleToCheck.Clone();
                }
            }
            if (toolBinding != null) { //Binding doesn't always drop, only if the durability is above the threshold, and then if it's below, it breaks and if made of metal, drops some bits
                BindingStatDefines bindingStats = HaftModSystem.Stats.BindingStats.Get(HaftModSystem.Stats.BindingParts.Get(toolBinding.Collectible.Code.Path).bindingStatTag);
                float bindingPercentRemains = (float)(remainingBindingDur) / (float)(brokenToolStack.GetToolbindingMaxDurability());
                if (bindingPercentRemains < bindingStats.recoveryPercent) { //If the remaining HP percent is less then the recovery percent, the binding is used up.
                    toolBinding = null; //Set it back to null to prevent dropping anything later! And then to see if Bits should drop!
                }

                if (toolBinding == null && bindingStats.isMetal) {
                    int numBits;
                    if (world.Rand.NextDouble() < 0.5) {
                        numBits = HaftConstants.NumBitsReturnMinimum;
                    } else {
                        numBits = HaftConstants.NumBitsReturnMinimum + 1;
                    }
                    bitsDrop = new ItemStack(world.GetItem(new AssetLocation("game:metalbit-" + bindingStats.metalType)), numBits);
                }
            }

            if (HaftModSystem.Config.DebugMessages && world.Api.Side.IsServer()) {
                HaftModSystem.Logger.Debug("Tool broke!");
                HaftModSystem.Logger.Debug("The tool head is " + toolHead.Collectible.Code.ToString());
                HaftModSystem.Logger.Debug("Head has durability: " + remainingHeadDur);
                HaftModSystem.Logger.Debug("Tool Handle is " + (toolHandle?.Collectible.Code.ToString()));
                HaftModSystem.Logger.Debug("Handle has durability: " + remainingHandleDur);
                HaftModSystem.Logger.Debug("Tool Binding is " + (toolBinding?.Collectible.Code.ToString()));
                HaftModSystem.Logger.Debug("Binding has durability: " + remainingBindingDur);
            }

            //Handle any mod compat drops here for when a tool breaks!
            if (headBroke && world.Api.ModLoader.IsModEnabled("canjewelry")) {
                CanJewelryCompat.HandleGemDropsForJewelry(byEntity, toolHead);
            }

            //Whether the tool came apart around a head that survived, rather than ending outright. Only this case
            //empties the slot here; a head that broke, or one that was never more than the placeholder, leaves the
            //stack for the caller to destroy. The handle and binding are handed back either way, since they wear
            //out on their own schedule and a head reaching zero is no reason to lose a treated handle with it.
            bool toolFellApart = !headBroke && !headIsPlaceholder;

            //toolFellApart decides everything below: it empties the slot and returns the head, and it is also what
            //gates the durability zeroing at the end. A tool that fell apart skips that zeroing, so if this is true
            //on a slot with no inventory, the projectile is about to read a durability nobody decremented.
            if (HaftModSystem.Config.DebugMessages && world.Api.Side.IsServer()) {
                HaftModSystem.Logger.Debug("[BreakTrace] headBroke is now " + headBroke + " (set true above if head durability hit 0)");
                HaftModSystem.Logger.Debug("[BreakTrace] toolFellApart = !headBroke && !headIsPlaceholder = " + toolFellApart);
                if (toolFellApart && itemslot.Inventory == null) {
                    HaftModSystem.Logger.Debug("[BreakTrace] WARNING: fell apart on a slot with no inventory. The zeroing at the end is skipped in this case, so whatever holds this stack keeps it.");
                }
            }

            EntityPlayer player = byEntity as EntityPlayer;
            if (player != null) {
                //Try to give the player each part, if given successfully, set the stack to null again to represent this
                if (toolFellApart) {
                    gaveHead = player.TryGiveItemStack(toolHead);
                }
                if (toolHandle != null) {
                    IModularPartRenderer behavior = (IModularPartRenderer)toolHandle.Collectible.CollectibleBehaviors.FirstOrDefault(b => (b as IModularPartRenderer) != null);
                    behavior.ResetRotationAndOffset(toolHandle);
                    gaveHandle = player.TryGiveItemStack(toolHandle);
                }
                if (toolBinding != null) {
                    gaveBinding = player.TryGiveItemStack(toolBinding);
                } else if (bitsDrop != null && player.TryGiveItemStack(bitsDrop)) {
                    bitsDrop = null;
                }

                if (toolFellApart) { //Move this inside the loop and AFTER giving the itemstack to prevent it from ending up in the same slot that the tool was in. This prevents things like the Treecutting code from falsely assuming the axe is not broke when it actually is.
                    itemslot.Itemstack = null; //Actually 'break' the original item, but only if the head part isn't broken yet. Handle the 'falling apart' of the tools here, but let the 'breaking' happen elsewhere if the head DID fully break.

                    if (refillSlot && toolObject.Tool.HasValue) { //Attempt to refill the slot with a same tool only after the slot is emptied, otherwise it won't succeed.
                        string ident = toolObject.Attributes?["slotRefillIdentifier"].ToString();
                        toolObject.RefillSlotIfEmpty(itemslot, byEntity as EntityAgent, (ItemStack stack) => (ident == null) ? (stack.Collectible.Tool == toolObject.Tool) : (stack.ItemAttributes?["slotRefillIdentifier"]?.ToString() == ident));
                    }

                    if (world.Side.IsServer()) {
                        world.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), player);
                    }
                }
            } else {
                if (toolFellApart) { //This needs to be in here as well since no matter what, this needs to run
                    itemslot.Itemstack = null; //Actually 'break' the original item, but only if the head part isn't broken yet. Handle the 'falling apart' of the tools here, but let the 'breaking' happen elsewhere if the head DID fully break.
                    if (world.Side.IsServer()) {
                        world.PlaySoundAt(new AssetLocation("sounds/effect/toolbreak"), byEntity.Pos.X, byEntity.Pos.Y, byEntity.Pos.Z, null, 1f, 16f);
                    }
                }
            }

            //Drop any remaining tools not given to the player into the world. byEntity is where the tool was when
            //it came apart, which for a thrown spear is the projectile at its impact point rather than the thrower,
            //so a spear that gives out mid-flight leaves its parts where it landed.
            if (toolFellApart && !gaveHead) {
                world.SpawnItemEntity(toolHead, byEntity.Pos.XYZ);
            }
            if (toolHandle != null && !gaveHandle) {
                IModularPartRenderer behavior = (IModularPartRenderer)toolHandle.Collectible.CollectibleBehaviors.FirstOrDefault(b => (b as IModularPartRenderer) != null);
                behavior.ResetRotationAndOffset(toolHandle);
                world.SpawnItemEntity(toolHandle, byEntity.Pos.XYZ);
            }
            if (toolBinding != null && !gaveBinding) {
                world.SpawnItemEntity(toolBinding, byEntity.Pos.XYZ);
            } else if (bitsDrop != null) {
                world.SpawnItemEntity(bitsDrop, byEntity.Pos.XYZ);
            }

            //A tool that fell apart has already been taken out of the slot above. Anything else - a head that
            //broke, or a head that was never more than the placeholder - has ended and still sits there, so the
            //caller destroys it. Answering with the slot's state rather than with the headBroke that came in is
            //what keeps a placeholder-headed tool from surviving the damage that finished it.
            //
            //Except that destroying the slot only reaches a tool held in an inventory. A thrown spear is damaged
            //through a DummySlot the projectile built around its own ProjectileStack, and emptying that dummy
            //leaves the projectile's reference untouched: it re-reads the same stack, finds durability on it, and
            //both keeps flying and stays collectible - handing back a whole spear beside the parts dropped above.
            //Zeroing the stack is what the projectile actually reads, through the "durability" attribute that
            //Haft's own head durability is stored in, so it sees a spent item and despawns itself.
            if (!toolFellApart && itemslot.Inventory == null) {
                toolObject.SetDurability(brokenToolStack, 0);

                if (HaftModSystem.Config.DebugMessages && world.Api.Side.IsServer()) {
                    HaftModSystem.Logger.Debug("[BreakTrace] exit: zeroed the stack (no inventory, did not fall apart). Returning false.");
                    HaftModSystem.Logger.Debug("[BreakTrace] vanilla durability after zeroing: " + toolObject.GetRemainingDurability(brokenToolStack) + " - a projectile despawns only if this reads 0.");
                }

                return false;
            }

            if (HaftModSystem.Config.DebugMessages && world.Api.Side.IsServer()) {
                HaftModSystem.Logger.Debug("[BreakTrace] exit: no zeroing done. toolFellApart=" + toolFellApart + ", inventory " + (itemslot.Inventory == null ? "NULL" : "present") + ", slot now holds " + (itemslot.Itemstack == null ? "nothing" : itemslot.Itemstack.Collectible.Code.ToString()));
                HaftModSystem.Logger.Debug("[BreakTrace] vanilla durability on the stack at exit: " + toolObject.GetRemainingDurability(brokenToolStack) + " - a projectile despawns only if this reads 0.");
                HaftModSystem.Logger.Debug("[BreakTrace] returning " + (!toolFellApart) + " (true means the caller destroys the slot).");
            }

            return !toolFellApart;
        }

        //A tool's durability as the game sees it is the head's, scaled up by the config multiplier. Both tool
        //behaviors report it the same way, so the multiplier lives in one place rather than in each of them.
        public static int ScaleToHeadDurability(int durability) {
            return (int)((double)durability * HaftModSystem.Config.HeadDurabilityMult);
        }

        //Everything a tinkered tool's parts contribute to mining speed: its sharpness, plus the handle, grip and
        //material bonus stored on the stack. Both the swing path and the tooltip read this, so the number shown is
        //by construction the number applied.
        public static float TinkeredToolMiningSpeedMultiplier(ItemStack itemstack) {
            var speedMult = SharpnessMiningSpeedMultiplier(itemstack);
            return speedMult + (speedMult * itemstack.GetSpeedBonus());
        }

        //How much a tool's sharpness alone changes its mining speed. A keen edge cuts faster and a dull one drags;
        //between those bands sharpness costs nothing. A tinkered tool adds its handle's speed bonus on top of this,
        //which is the only way the two tool kinds differ here.
        public static float SharpnessMiningSpeedMultiplier(ItemStack itemstack) {
            float speedMult = 1f;
            var sharpnessPer = itemstack.GetToolSharpnessPercent();
            if (sharpnessPer >= 0.9) {
                speedMult += speedMult * HaftConstants.HighSharpnessSpeedBonusMult;
            } else if (sharpnessPer <= 0.33) {
                speedMult += speedMult * HaftConstants.LowSharpnessSpeedMalusMult;
            }

            return speedMult;
        }

        public static ItemWhetstone WhetstoneInOffhand(EntityAgent byEntity) {
            if (byEntity.LeftHandItemSlot.Empty) {
                return null;
            }
            var offhandItem = byEntity.LeftHandItemSlot?.Itemstack?.Item;
            return offhandItem as ItemWhetstone;
        }

        public static bool IsValidHead(ItemStack stack) {
            if (stack == null || stack.Class != EnumItemClass.Item) {
                return false;
            }
            return stack.Collectible.HasBehavior<CollectibleBehaviorToolHead>(); //HaftConstants.HaftHeadItemTag.isPresentIn(ref stack.Item.Tags);
        }

        public static bool ValidHandleInOffhand(EntityAgent byEntity) {
            return IsValidHandle(byEntity.LeftHandItemSlot?.Itemstack);
        }

        public static bool IsValidHandle(ItemStack stack) {
            if (stack == null || stack.Class != EnumItemClass.Item || stack.HasWetTreatment()) {
                return false;
            }
            return stack.Collectible.HasBehavior<CollectibleBehaviorToolHandle>(); //HaftConstants.HaftHandleItemTag.isPresentIn(ref stack.Item.Tags);
        }

        public static bool ValidBindingInOffhand(EntityAgent byEntity) {
            return IsValidBinding(byEntity.LeftHandItemSlot?.Itemstack);
        }

        //Answers "is this an acceptable thing to be holding where a binding is optional" - so nothing at all is a
        //yes, because a tool crafts perfectly well without a binding. Only ask this about an offhand slot or a
        //crafting input that is allowed to be absent. To ask whether a stack IS a binding, use IsBindingItem.
        public static bool IsValidBinding(ItemStack stack) {
            if (stack == null) {
                return true;
            }

            var liquidContainer = stack.Block as BlockLiquidContainerBase;
            if (liquidContainer == null) {
                return stack.Collectible.HasBehavior<CollectibleBehaviorToolBinding>();
            }

            //A container counts only when it actually holds enough of a binding liquid to use.
            var liquid = liquidContainer.GetContent(stack);
            var bindingPart = liquid == null ? null : HaftModSystem.Stats.BindingParts.TryGetValue(liquid.Collectible.Code.Path);
            return bindingPart != null && liquidContainer.GetCurrentLitres(stack) >= bindingPart.litersUsed;
        }

        //Answers "is this stack a binding" - nothing is not a binding. Use this when picking a binding out of a set
        //of items, where treating an absent stack as a match would pick the wrong slot. IsValidBinding answers the
        //other question, whether an optional binding slot holds something acceptable, and says yes to nothing.
        public static bool IsBindingItem(ItemStack stack) {
            if (stack == null) {
                return false;
            }

            return IsValidBinding(stack);
        }

        //The binding a stack represents. A binding can be an item held directly or a liquid inside a container, and
        //every caller that wants the binding itself rather than what is holding it has to unwrap the second case.
        //Tested against ILiquidInterface rather than BlockLiquidContainerBase so a container from another mod that
        //implements the interface without deriving from that class still gives up its contents.
        public static ItemStack GetBindingContent(ItemStack stack) {
            var container = stack?.Block as ILiquidInterface;
            if (container != null) {
                return container.GetContent(stack);
            }

            return stack;
        }

        //Takes the binding out of the slot it was supplied from: a measured draw for a liquid, one item otherwise.
        //How much liquid a binding uses is a property of the binding part, so a container is never emptied by a
        //fixed amount.
        public static void ConsumeBindingFromSlot(ItemSlot bindingSlot) {
            if (bindingSlot == null || bindingSlot.Empty) {
                return;
            }

            var liquidContainer = bindingSlot.Itemstack.Block as BlockLiquidContainerBase;
            if (liquidContainer != null) {
                var binding = liquidContainer.GetContent(bindingSlot.Itemstack);
                var bindingPart = HaftModSystem.Stats.BindingParts.TryGetValue(binding.Collectible.Code.Path);
                liquidContainer.TryTakeLiquid(bindingSlot.Itemstack, bindingPart.litersUsed);
            } else {
                bindingSlot.TakeOut(1);
            }

            bindingSlot.MarkDirty();
        }

        //Runs the crafting hooks a tool built outside the grid would otherwise never see. Both ways of building a
        //tinkered tool - assembled in hand from a bundle, or hammered together on the workbench - go through this, so
        //a mod hooking OnCreatedByCrafting sees the same call either way.
        //
        //Returns null when the head has no tool registered against it, which is the one case neither caller can
        //proceed from.
        //
        //ConsumeCraftingIngredients is called for ItemRarity's sake. In the grid it runs after OnCreatedByCrafting,
        //when the player takes the item, rather than before it as here.
        //buildRender runs after the tool stack exists but BEFORE the crafting hooks, because it reads the input part
        //stacks and ConsumeCraftingIngredients is free to consume them.
    }
}
