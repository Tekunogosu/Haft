using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toolsmith.Compat;
using Toolsmith.Config;
using Toolsmith.Utils;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.ServerMods.NoObf;
using Toolsmith.ToolTinkering.Items;
using Toolsmith.ToolTinkering.Behaviors;
using Toolsmith.Client;
using System.Reflection.Metadata.Ecma335;
using Vintagestory.GameContent;
using Vintagestory.API.Config;
using Toolsmith.ToolTinkering.Drawbacks;
using Vintagestory.API.Datastructures;
using Newtonsoft.Json.Linq;

namespace Toolsmith.ToolTinkering {
    //This is beginning to hold the MEAT of the whole tinkering system. It has various helper functions that are being used in multiple places to help keep everything just running the single code calls and ensuring it isn't spaghetti while I add more ways to do the same things.
    public static class TinkeringUtility {

        static int[] sharpnessColors = new int[11] {
            ColorUtil.Hex2Int("#7e0279"),
            ColorUtil.Hex2Int("#7a3299"),
            ColorUtil.Hex2Int("#6f4eb6"),
            ColorUtil.Hex2Int("#5e67ce"),
            ColorUtil.Hex2Int("#457fe2"),
            ColorUtil.Hex2Int("#1995f0"),
            ColorUtil.Hex2Int("#00abf8"),
            ColorUtil.Hex2Int("#00bffd"),
            ColorUtil.Hex2Int("#00d3fd"),
            ColorUtil.Hex2Int("#00e6fb"),
            ColorUtil.Hex2Int("#43f8f8")
        };
        static int[] unpleasantGradient = new int[11] { //It's very unpleasant.
            ColorUtil.Hex2Int("#9e5400"),
            ColorUtil.Hex2Int("#c34523"),
            ColorUtil.Hex2Int("#e5274a"),
            ColorUtil.Hex2Int("#fe007c"),
            ColorUtil.Hex2Int("#ff00b8"),
            ColorUtil.Hex2Int("#fa35fd"),
            ColorUtil.Hex2Int("#ff5fa3"),
            ColorUtil.Hex2Int("#ff746a"),
            ColorUtil.Hex2Int("#fb8e00"),
            ColorUtil.Hex2Int("#aebb00"),
            ColorUtil.Hex2Int("#23d726")
        };
        static int[] monhunGradiant = new int[11] {
            ColorUtil.Hex2Int("#ff0f00"),
            ColorUtil.Hex2Int("#ff4b00"),
            ColorUtil.Hex2Int("#ff6b00"),
            ColorUtil.Hex2Int("#ffb300"),
            ColorUtil.Hex2Int("#fff700"),
            ColorUtil.Hex2Int("#b4fd00"),
            ColorUtil.Hex2Int("#24ff00"),
            ColorUtil.Hex2Int("#009aab"),
            ColorUtil.Hex2Int("#0000FF"),
            ColorUtil.Hex2Int("#8080ff"),
            ColorUtil.Hex2Int("#ffffff")
        };
        static int[] flatLevelMonHunColors = new int[7] {
            ColorUtil.Hex2Int("#ff0f00"),
            ColorUtil.Hex2Int("#ff6b00"),
            ColorUtil.Hex2Int("#fff700"),
            ColorUtil.Hex2Int("#24ff00"),
            ColorUtil.Hex2Int("#50b0ff"),
            ColorUtil.Hex2Int("#ffffff"),
            ColorUtil.Hex2Int("#c050ff")
        };
        static int[] SharpnessColorGradient = null;

        //This just sets the color gradiant for the Sharpness Bar. Only run this on the Client. This bit was copied over from GuiStyle vanilla code! And the hex values adjusted.
        public static void InitializeSharpnessColorGradient() {
            var gradiantChoice = SelectGradiantForSharpness();

            SharpnessColorGradient = new int[100];
            for (int i = 0; i < 10; i++) {
                for (int j = 0; j < 10; j++) {
                    SharpnessColorGradient[10 * i + j] = ColorUtil.ColorOverlay(gradiantChoice[i], gradiantChoice[i + 1], (float)j / 10f);
                }
            }
        }

        public static bool GradiantNeedsInit() {
            return SharpnessColorGradient == null;
        }

        private static int[] SelectGradiantForSharpness() {
            switch (ToolsmithModSystem.GradientSelection) {
                case 0:
                    return sharpnessColors;
                case 1:
                    return unpleasantGradient;
                case 2:
                    return monhunGradiant;
                default:
                    return sharpnessColors;
            }
        }

        public static bool ShouldRenderSharpnessBar(ItemStack item) {
            if (!item.HasToolCurrentSharpness() || !item.HasToolMaxSharpness()) { //Since technically this accesses the same attribute as the Part variants, no real edit is needed here.
                return false;
            }
            if ((item.Collectible.HasBehavior<CollectibleBehaviorTinkeredTools>() || item.Collectible.HasBehavior<CollectibleBehaviorSmithedTools>() || IsValidHead(item)) && !item.Collectible.HasBehavior<CollectibleBehaviorToolBlunt>()) { //Just add a check for a toolhead
                return item.GetToolCurrentSharpness() != item.GetToolMaxSharpness();
            } else {
                return false;
            }
        }

        public static int GetItemSharpnessColor(ItemStack item) {
            int maxSharpness = item.GetToolMaxSharpness();
            if (maxSharpness == 0) {
                return 0;
            }

            int num = GameMath.Clamp(100 * item.GetToolCurrentSharpness() / maxSharpness, 0, 99);
            return SharpnessColorGradient[num];
        }

        public static int GetFlatItemSharpnessColor(int index) {
            return flatLevelMonHunColors[index];
        }

        public static int ToolsmithGetItemDamageColor(ItemStack item) {
            if (item.Collectible.HasBehavior<CollectibleBehaviorTinkeredTools>() || IsValidHead(item) || item.Collectible.HasBehavior<CollectibleBehaviorToolHandle>()) {
                var max = FindLowestMaxDurabilityForBar(item);
                if (max == 0) {
                    return 0;
                }

                int num = GameMath.Clamp(100 * FindLowestCurrentDurabilityForBar(item) / max, 0, 99);
                return GuiStyle.DamageColorGradient[num];
            } else {
                return item.Collectible.GetItemDamageColor(item);
            }
        }

        //For rendering the durability bar to be used in the transpiler. Generally for just Tinkered Tools and Parts here, smithed ones can use the default!
        public static int FindLowestCurrentDurabilityForBar(ItemStack itemStack) {
            if (itemStack.Collectible.HasBehavior<CollectibleBehaviorTinkeredTools>()) {
                if (!itemStack.HasToolheadCurrentDurability() || !itemStack.HasToolhandleCurrentDurability() || !itemStack.HasToolbindingCurrentDurability()) {
                    return itemStack.Collectible.GetRemainingDurability(itemStack);
                }
                var head = itemStack.GetToolheadCurrentDurability();
                var handle = itemStack.GetToolhandleCurrentDurability();
                var binding = itemStack.GetToolbindingCurrentDurability();
                int lowest;

                if (binding < handle) {
                    lowest = binding;
                } else {
                    lowest = handle;
                }
                if (lowest < head) {
                    return lowest;
                } else {
                    return head;
                }
            } else if (IsValidHead(itemStack) || IsValidHandle(itemStack)) {
                return itemStack.GetPartCurrentDurability();
            } else {
                return itemStack.Collectible.GetRemainingDurability(itemStack);
            }
        }

        //Used just like the above, but for the max durabilities!
        public static int FindLowestMaxDurabilityForBar(ItemStack itemStack) {
            if (itemStack.Collectible.HasBehavior<CollectibleBehaviorTinkeredTools>()) {
                if (!itemStack.HasToolhandleMaxDurability() || !itemStack.HasToolbindingMaxDurability()) {
                    return itemStack.Collectible.GetMaxDurability(itemStack);
                }
                var head = itemStack.GetToolheadMaxDurability();
                var handle = itemStack.GetToolhandleMaxDurability();
                var binding = itemStack.GetToolbindingMaxDurability();
                int lowest;

                if (binding < handle) {
                    lowest = binding;
                } else {
                    lowest = handle;
                }
                if (lowest < head) {
                    return lowest;
                } else {
                    return head;
                }
            } else if (IsValidHead(itemStack) || IsValidHandle(itemStack)) {
                return itemStack.GetPartMaxDurability();
            } else {
                return itemStack.Collectible.GetMaxDurability(itemStack);
            }
        }

        /// <summary>
        /// Replaces the tool tier and mining speed lines vanilla wrote with ones laid out the way the rest of a
        /// Toolsmith tooltip is: one material per row, in a monospace column, with the speed colored.
        /// </summary>
        /// <remarks>
        /// Vanilla builds its mining speed line by appending each material onto one running line, so the lang key
        /// only supplies the "Mining Speed: " prefix - there is no way to split the materials apart by overriding
        /// the key alone. The line is taken back out and rebuilt from GetMiningSpeeds instead, which also avoids
        /// parsing numbers back out of text that has already been formatted for display.
        ///
        /// The 1.1 floor is vanilla's own: anything at or below it is not fast enough to be worth a line.
        ///
        /// Both the tinkered and the smithed tool tooltips need this, which is why it lives here rather than in
        /// either of them.
        /// </remarks>
        public static void ReplaceVanillaToolSpeedLines(ItemSlot inSlot, StringBuilder tooltip) {
            var collectible = inSlot.Itemstack.Collectible;
            var miningSpeeds = collectible.GetMiningSpeeds(inSlot);

            StringHelpers.RemoveTooltipLineStartingWith(tooltip, Lang.Get("item-tooltip-miningspeed"));
            StringHelpers.RemoveTooltipLineStartingWith(tooltip, Lang.Get("Tool Tier: {0}", collectible.GetToolTier(inSlot)));

            if (miningSpeeds == null || miningSpeeds.Count == 0) {
                return;
            }

            tooltip.AppendLine(Lang.Get("toolsmithtooltier", collectible.GetToolTier(inSlot)));

            var speedModifier = collectible.GetMiningSpeedModifier(inSlot.Itemstack);
            bool wroteHeader = false;
            foreach (var materialSpeed in miningSpeeds) {
                var speed = materialSpeed.Value * speedModifier;
                if (speed < 1.1) {
                    continue;
                }

                if (!wroteHeader) {
                    tooltip.AppendLine(Lang.Get("toolsmithminingspeedheader"));
                    wroteHeader = true;
                }
                tooltip.AppendLine(Lang.Get("toolsmithminingspeedrow", Lang.Get(materialSpeed.Key.ToString()).PadRight(10), StringHelpers.ColorForMiningSpeed(speed), speed.ToString("#.#")));
            }
        }

        //Helper method to centralize the checks for if something should attempt to access this Slot or Itemstack generally for purposes of handling or accessing any Attribute data. This is important to prevent it from assigning attribute data to something that shouldn't get it, IE trader inventories or creative before it's actually pulled out.
        public static bool ShouldNotAccessStats(ItemSlot slot) {
            if (slot == null) {
                return true;
            }

            if (slot.Itemstack == null || slot.Inventory == null) {
                return true;
            }

            if (slot.Inventory.GetType() == typeof(DummyInventory) || slot.Inventory.GetType() == typeof(CreativeInventoryTab) || slot.Inventory.GetType() == typeof(InventoryTrader)) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Hands a broken tool's surviving parts back and decides what becomes of the stack that held them.
        /// Returns true when the slot still holds a tool that has ended and the caller must destroy, and false
        /// when this call already emptied the slot itself.
        /// </summary>
        /// <remarks>
        /// The slot is not always a player's. A thrown spear is damaged through a DummySlot wrapping the
        /// projectile's own stack, with the projectile (or the entity it struck) passed as byEntity, so nothing
        /// here may assume an inventory to give to or a player to give to.
        /// </remarks>
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
            if (ToolsmithModSystem.Config.DebugMessages && world.Api.Side.IsServer()) {
                ToolsmithModSystem.Logger.Debug("[BreakTrace] --- HandleBrokenTinkeredTool entry for " + toolObject.Code + " ---");
                ToolsmithModSystem.Logger.Debug("[BreakTrace] slot is " + itemslot.GetType().Name + ", inventory is " + (itemslot.Inventory == null ? "NULL (not held - projectile or dummy)" : itemslot.Inventory.GetType().Name));
                ToolsmithModSystem.Logger.Debug("[BreakTrace] byEntity is " + (byEntity == null ? "null" : byEntity.GetType().Name + " (" + byEntity.Code + ")"));
                ToolsmithModSystem.Logger.Debug("[BreakTrace] incoming head/handle/binding durability: " + remainingHeadDur + " / " + remainingHandleDur + " / " + remainingBindingDur);
                ToolsmithModSystem.Logger.Debug("[BreakTrace] incoming headBroke: " + headBroke + ", refillSlot: " + refillSlot + ", headIsPlaceholder: " + headIsPlaceholder);
                ToolsmithModSystem.Logger.Debug("[BreakTrace] vanilla durability on the stack right now: " + toolObject.GetRemainingDurability(brokenToolStack) + " (this is what a projectile reads to decide whether to despawn)");
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
                headBroke = true; //This right here might be key for compatability sake. The way I built the whole system runs off the assumption that the Tool's Head determines the tool.
                                  //Thus, it can be considered that a tool does not fully "break" in the vanilla sense until the Head itself breaks, it only "falls apart" ie: the tool head flies off the handle, there's possible durability left on both.
                                  //Because of this, always need to consider the possibility of dropping a handle or binder, but if the 'Head' is broken, we also want to run other mod's 'on damage' calls along with vanilla.
                                  //Anything below that checks for !headBroke is looking to see if the Tool should be "Broken" or simply "Fallen Apart" in this sense, if it's fallen apart, do similar checks to vanilla tool breaking locally here. Otherwise let Vanilla code deal with it, since it's all or nothing after this patch is done.
            }

            if (ToolsmithModSystem.Api.ModLoader.IsModEnabled("canjewelry")) {
                CanJewelryCompat.CheckAndHandleJewelryStatTransfer(brokenToolStack, toolHead);
            }

            if (remainingHandleDur > 0) {
                var handleToCheck = brokenToolStack.GetToolhandle();
                handleToCheck.SetPartCurrentDurability(remainingHandleDur);
                handleToCheck.SetPartMaxDurability(brokenToolStack.GetToolhandleMaxDurability());
                var handlePercentDamage = handleToCheck.GetPartRemainingHPPercent();
                float comparedPercent = 0.0f;
                if (handleToCheck.Collectible.Code != ToolsmithConstants.DefaultHandleCode && handleToCheck.Collectible.Code != ToolsmithConstants.BoneHandleCode) { //Is this handle not a stick or bone?
                    comparedPercent = ToolsmithConstants.OtherHandleFailurePercent;
                } else {
                    comparedPercent = ToolsmithConstants.StickAndBoneFailurePercent;
                }

                if (handlePercentDamage > comparedPercent) {
                    toolHandle = handleToCheck.Clone();
                    /*if (toolHandle.HasMultiPartRenderTree()) {

                    }*/
                }
            }
            if (toolBinding != null) { //Binding doesn't always drop, only if the durability is above the threshold, and then if it's below, it breaks and if made of metal, drops some bits
                BindingStatDefines bindingStats = ToolsmithModSystem.Stats.BindingStats.Get(ToolsmithModSystem.Stats.BindingParts.Get(toolBinding.Collectible.Code.Path).bindingStatTag);
                float bindingPercentRemains = (float)(remainingBindingDur) / (float)(brokenToolStack.GetToolbindingMaxDurability());
                if (bindingPercentRemains < bindingStats.recoveryPercent) { //If the remaining HP percent is less then the recovery percent, the binding is used up.
                    toolBinding = null; //Set it back to null to prevent dropping anything later! And then to see if Bits should drop!
                }

                if (toolBinding == null && bindingStats.isMetal) {
                    int numBits;
                    if (world.Rand.NextDouble() < 0.5) {
                        numBits = ToolsmithConstants.NumBitsReturnMinimum;
                    } else {
                        numBits = ToolsmithConstants.NumBitsReturnMinimum + 1;
                    }
                    bitsDrop = new ItemStack(world.GetItem(new AssetLocation("game:metalbit-" + bindingStats.metalType)), numBits);
                }
            }

            if (ToolsmithModSystem.Config.DebugMessages && world.Api.Side.IsServer()) {
                ToolsmithModSystem.Logger.Debug("Tool broke!");
                ToolsmithModSystem.Logger.Debug("The tool head is " + toolHead.Collectible.Code.ToString());
                ToolsmithModSystem.Logger.Debug("Head has durability: " + remainingHeadDur);
                ToolsmithModSystem.Logger.Debug("Tool Handle is " + (toolHandle?.Collectible.Code.ToString()));
                ToolsmithModSystem.Logger.Debug("Handle has durability: " + remainingHandleDur);
                ToolsmithModSystem.Logger.Debug("Tool Binding is " + (toolBinding?.Collectible.Code.ToString()));
                ToolsmithModSystem.Logger.Debug("Binding has durability: " + remainingBindingDur);
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
            if (ToolsmithModSystem.Config.DebugMessages && world.Api.Side.IsServer()) {
                ToolsmithModSystem.Logger.Debug("[BreakTrace] headBroke is now " + headBroke + " (set true above if head durability hit 0)");
                ToolsmithModSystem.Logger.Debug("[BreakTrace] toolFellApart = !headBroke && !headIsPlaceholder = " + toolFellApart);
                if (toolFellApart && itemslot.Inventory == null) {
                    ToolsmithModSystem.Logger.Debug("[BreakTrace] WARNING: fell apart on a slot with no inventory. The zeroing at the end is skipped in this case, so whatever holds this stack keeps it.");
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
            //Toolsmith's own head durability is stored in, so it sees a spent item and despawns itself.
            if (!toolFellApart && itemslot.Inventory == null) {
                toolObject.SetDurability(brokenToolStack, 0);

                if (ToolsmithModSystem.Config.DebugMessages && world.Api.Side.IsServer()) {
                    ToolsmithModSystem.Logger.Debug("[BreakTrace] exit: zeroed the stack (no inventory, did not fall apart). Returning false.");
                    ToolsmithModSystem.Logger.Debug("[BreakTrace] vanilla durability after zeroing: " + toolObject.GetRemainingDurability(brokenToolStack) + " - a projectile despawns only if this reads 0.");
                }

                return false;
            }

            if (ToolsmithModSystem.Config.DebugMessages && world.Api.Side.IsServer()) {
                ToolsmithModSystem.Logger.Debug("[BreakTrace] exit: no zeroing done. toolFellApart=" + toolFellApart + ", inventory " + (itemslot.Inventory == null ? "NULL" : "present") + ", slot now holds " + (itemslot.Itemstack == null ? "nothing" : itemslot.Itemstack.Collectible.Code.ToString()));
                ToolsmithModSystem.Logger.Debug("[BreakTrace] vanilla durability on the stack at exit: " + toolObject.GetRemainingDurability(brokenToolStack) + " - a projectile despawns only if this reads 0.");
                ToolsmithModSystem.Logger.Debug("[BreakTrace] returning " + (!toolFellApart) + " (true means the caller destroys the slot).");
            }

            return !toolFellApart;
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
            return stack.Collectible.HasBehavior<CollectibleBehaviorToolHead>(); //ToolsmithConstants.ToolsmithHeadItemTag.isPresentIn(ref stack.Item.Tags);
        }

        public static bool ValidHandleInOffhand(EntityAgent byEntity) {
            return IsValidHandle(byEntity.LeftHandItemSlot?.Itemstack);
        }

        public static bool IsValidHandle(ItemStack stack) {
            if (stack == null || stack.Class != EnumItemClass.Item || stack.HasWetTreatment()) {
                return false;
            }
            return stack.Collectible.HasBehavior<CollectibleBehaviorToolHandle>(); //ToolsmithConstants.ToolsmithHandleItemTag.isPresentIn(ref stack.Item.Tags);
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

            if (stack.Collectible.ItemClass == EnumItemClass.Item) {
                return stack.Collectible.HasBehavior<CollectibleBehaviorToolBinding>(); //ToolsmithConstants.ToolsmithBindingItemTag.isPresentIn(ref stack.Item.Tags);
            } else {
                if (stack.Block as BlockLiquidContainerBase != null) {
                    var liquidContainer = stack.Block as BlockLiquidContainerBase;
                    var liquid = liquidContainer.GetContent(stack);
                    if (liquid != null) {
                        var bindingPart = ToolsmithModSystem.Stats.BindingParts.TryGetValue(liquid.Collectible.Code.Path);
                        if (bindingPart != null) {
                            return liquidContainer.GetCurrentLitres(stack) >= bindingPart.litersUsed;
                        }
                    }

                    return false;
                } else {
                    return stack.Collectible.HasBehavior<CollectibleBehaviorToolBinding>(); //ToolsmithConstants.ToolsmithBindingBlockTag.isPresentIn(ref stack.Block.Tags);
                }
            }
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

        public static void AssemblePartBundle(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel) {
            ItemStack bundle = new ItemStack(byEntity.World.GetItem(ToolsmithConstants.ToolBundleCode), 1);
            ItemSlot handleSlot = byEntity.LeftHandItemSlot;
            ItemStack head = slot.TakeOut(1);
            ItemStack handle = handleSlot.TakeOut(1); //Take out one here to not end up adding things to the initial stack. Whoops. That's why it was applying the render info to a full stack of handles.

            MultiPartRenderingHelpers.BuildToolRenderFromHeadAndHandle(bundle, head, handle);

            bundle.SetToolhead(head);
            bundle.SetToolhandle(handle); //Set the bundle's head and handle here!

            handleSlot.MarkDirty();
            ItemStack tempHolder = slot.Itemstack;
            slot.Itemstack = bundle; //Above holds the possible multiple-stacked Toolheads, this finally gives the crafted tool to slot that previously had the head(s)
            slot.MarkDirty();
            if (tempHolder != null) {
                if (!byEntity.TryGiveItemStack(tempHolder)) { //This should hopefully return any remainder!
                    byEntity.World.SpawnItemEntity(tempHolder, byEntity.Pos.XYZ);
                }
            }
        }

        public static void AssembleFullTool(ItemSlot bundleSlot, EntityAgent byEntity, BlockSelection blockSel) {
            CollectibleObject craftedTool;
            ItemStack head = bundleSlot.Itemstack.GetToolhead();
            var success = RecipeRegisterModSystem.TinkerToolGridRecipes.TryGetValue(head.Collectible.Code.ToString(), out craftedTool);
            if (success) {
                ItemStack craftedItemStack = new ItemStack(byEntity.World.GetItem(craftedTool.Code), 1); //Create the tool in question
                ItemSlot placeholderOutput = new ItemSlot(new DummyInventory(ToolsmithModSystem.Api));
                placeholderOutput.Itemstack = craftedItemStack;
                TreeAttribute applyQuenchable = new TreeAttribute();
                applyQuenchable.SetBool("applyquenchablebuffs", true);

                GridRecipe DummyRecipe = new() {
                    AverageDurability = false,
                    Output = new() {
                        ResolvedItemStack = craftedItemStack,
                        RecipeAttributes = new JsonObject(JToken.Parse(applyQuenchable.ToJsonToken()))
                    },
                    Name = new AssetLocation("toolsmith:inhandtinkertoolcrafting")
                };

                ItemStack handle = bundleSlot.Itemstack.GetToolhandle();
                ItemSlot headSlot = new ItemSlot(new DummyInventory(ToolsmithModSystem.Api));
                headSlot.Itemstack = head;
                ItemSlot handleSlot = new ItemSlot(new DummyInventory(ToolsmithModSystem.Api));
                handleSlot.Itemstack = handle;
                ItemSlot bindingSlot = byEntity.LeftHandItemSlot;

                ItemSlot[] inputSlots;
                if (bindingSlot.Empty) {
                    inputSlots = new ItemSlot[] { headSlot, handleSlot };
                } else {
                    inputSlots = new ItemSlot[] { headSlot, handleSlot, bindingSlot };
                }

                craftedItemStack.Collectible.ConsumeCraftingIngredients(inputSlots, placeholderOutput, DummyRecipe); //This line is needed because of ItemRarity, but at the same time, this is technically called _AFTER_ the 'onCreatedByCrafting' line, when the player actually clicks to take the item...
                                                                                                                     //Might be a good idea to reconsider when the whole Tinker Tool Crafting logic is called, but... Would require patching this call, and it's ONLY for Item Rarity so far, not exactly a priority by a long shot. Leaving this note incase something else uses this, but also probably not a big deal to make the change either?
                craftedItemStack.Collectible.OnCreatedByCrafting(inputSlots, placeholderOutput, DummyRecipe); //Hopefully call this just like it would if properly crafted in the grid!

                if (!bundleSlot.Itemstack.HasBundleHasGenericParts()) {
                    var successfulBindingAdd = false;
                    if (!bindingSlot.Empty) {
                        if (bindingSlot.Itemstack.Block as BlockLiquidContainerBase != null) {
                            var liquidContainer = bindingSlot.Itemstack.Block as BlockLiquidContainerBase;
                            var binding = liquidContainer.GetContent(bindingSlot.Itemstack);
                            successfulBindingAdd = MultiPartRenderingHelpers.AddBindingToExistingToolRender(bundleSlot.Itemstack, binding);
                        } else {
                            successfulBindingAdd = MultiPartRenderingHelpers.AddBindingToExistingToolRender(bundleSlot.Itemstack, bindingSlot.Itemstack);
                        }
                    }
                    if (!successfulBindingAdd) {
                        var toolType = MultiPartRenderingHelpers.GetToolTypeFromHeadShapePath(head.Item.Shape.Base.Path);
                        if (toolType != null && ToolsmithModSystem.ToolsWithWoodInBindingShapes.Contains(toolType)) {
                            MultiPartRenderingHelpers.AddWoodPartsOfBindingToExistingToolRender(bundleSlot.Itemstack);
                        }
                    }
                    craftedItemStack.SetMultiPartRenderTree(bundleSlot.Itemstack.GetMultiPartRenderTree());
                }

                if (!bindingSlot.Empty) {
                    if (bindingSlot.Itemstack.Block as BlockLiquidContainerBase != null) {
                        var liquidContainer = bindingSlot.Itemstack.Block as BlockLiquidContainerBase;
                        var binding = liquidContainer.GetContent(bindingSlot.Itemstack);
                        var bindingPart = ToolsmithModSystem.Stats.BindingParts.TryGetValue(binding.Collectible.Code.Path);
                        liquidContainer.TryTakeLiquid(bindingSlot.Itemstack, bindingPart.litersUsed);
                        bindingSlot.MarkDirty();
                    } else {
                        bindingSlot.TakeOut(1);
                        bindingSlot.MarkDirty();
                    }
                }
                bundleSlot.Itemstack = craftedItemStack;
                bundleSlot.MarkDirty();
            }
        }

        //Older code now, may be repurposed for the workbench later on.
        public static ItemSlot SearchForPossibleBindings(IPlayer player) { //Searches only the Hotbar just for efficiency sake! Also kinda ease of use that you don't have to dump EVERYTHING on the ground that might be a binding. Just store it in bags.
            IInventory hotbar = player.InventoryManager.GetHotbarInventory();
            foreach (var slot in hotbar.Where(s => s.Itemstack != null)) {
                if (slot.Itemstack.Collectible.HasBehavior<CollectibleBehaviorToolBinding>()) {
                    return slot;
                }
            }
            return null;
        }

        //Why a set of slots cannot be crafted into a tool. Every value other than None is something the player can
        //act on, so each one has its own message rather than a single "that didn't work" - being told the bench
        //refused without being told what to change is the thing this whole enum exists to prevent.
        public enum EnumCraftRefusal {
            None,
            NoHead,
            NoHandle,
            ExtraItems
        }

        //The same question CheckForValidTool answers, but phrased so the client can explain a refusal. Safe to call
        //on either side: it only reads the slots. The client raises the message and the server enforces the refusal,
        //because the message call is client-only and the craft itself must not be decided by the client.
        public static EnumCraftRefusal WhyCannotCraftTool(ItemSlot[] slots) {
            var sortedSlots = CheckForValidTool(slots);
            if (sortedSlots == null) {
                foreach (var slot in slots) {
                    if (IsValidHead(slot.Itemstack)) {
                        return EnumCraftRefusal.NoHandle; //A head is present, so the handle is what is missing.
                    }
                }
                return EnumCraftRefusal.NoHead;
            }

            if (slots.Length > sortedSlots.Length) { //Every slot that is not one of the parts found is in the way.
                return EnumCraftRefusal.ExtraItems;
            }

            return EnumCraftRefusal.None;
        }

        public static string GetCraftRefusalMessage(EnumCraftRefusal refusal) {
            switch (refusal) {
                case EnumCraftRefusal.NoHead:
                    return Lang.Get("toolsmith:workbench-needs-head");
                case EnumCraftRefusal.NoHandle:
                    return Lang.Get("toolsmith:workbench-needs-handle");
                case EnumCraftRefusal.ExtraItems:
                    return Lang.Get("toolsmith:workbench-extra-items");
                default:
                    return null;
            }
        }

        //Send it an array of itemslots, and it will see if it is valid for crafting a tool, then return the ItemStacks in a sorted array. Head -> Handle -> (optional) Binding
        public static ItemSlot[] CheckForValidTool(ItemSlot[] slots) {
            bool foundHead = false;
            int headSlot = -1;
            bool foundHandle = false;
            int handleSlot = -1;
            bool foundBinding = false;
            int bindingSlot = -1;

            for (int i = 0; i < slots.Length; i++) {
                if (!foundHead && IsValidHead(slots[i].Itemstack)) {
                    foundHead = true;
                    headSlot = i;
                }
                if (!foundHandle && IsValidHandle(slots[i].Itemstack)) {
                    foundHandle = true;
                    handleSlot = i;
                }
                //IsBindingItem, not IsValidBinding: the latter calls an absent stack an acceptable binding, which is
                //the right answer for an optional offhand slot and the wrong one here, where it would claim the
                //first empty slot as the binding and hand back a slot with nothing in it to craft from.
                if (!foundBinding && IsBindingItem(slots[i].Itemstack)) {
                    foundBinding = true;
                    bindingSlot = i;
                }
            }

            if (!foundHead || !foundHandle) {
                return null;
            }

            //Sized to the parts actually found rather than to how many slots were searched, so the length tells the
            //caller both whether a binding is present and, by comparison against the incoming count, whether
            //anything else is sitting on the bench. WhyCannotCraftTool reads that difference to explain a refusal.
            if (foundBinding) {
                return new ItemSlot[] { slots[headSlot], slots[handleSlot], slots[bindingSlot] };
            }

            return new ItemSlot[] { slots[headSlot], slots[handleSlot] };
        }

        //Send it an array of slots, and it will try to craft a tool from the parts! Otherwise it will return null.
        public static ItemStack TryCraftToolFromSlots(ItemSlot[] slots, IWorldAccessor world, BlockSelection blockSel) {
            //However many slots come in, CheckForValidTool decides what is craftable from them and hands back just
            //the parts. Refusing on the incoming count instead would turn a bench holding a head, a handle and one
            //unrelated item into a bench that silently does nothing when the hammer comes down.
            var sortedSlots = CheckForValidTool(slots);

            if (sortedSlots == null) {
                return null;
            }

            CollectibleObject craftedTool;
            var success = RecipeRegisterModSystem.TinkerToolGridRecipes.TryGetValue(sortedSlots[0].Itemstack.Collectible.Code.ToString(), out craftedTool);
            if (success) {
                ItemStack craftedItemStack = new ItemStack(world.GetItem(craftedTool.Code), 1); //Create the tool in question
                ItemSlot placeholderOutput = new ItemSlot(new DummyInventory(world.Api));
                placeholderOutput.Itemstack = craftedItemStack;
                TreeAttribute applyQuenchable = new TreeAttribute();
                applyQuenchable.SetBool("applyquenchablebuffs", true);

                GridRecipe DummyRecipe = new() {
                    AverageDurability = false,
                    Output = new() {
                        ResolvedItemStack = craftedItemStack,
                        RecipeAttributes = new JsonObject(JToken.Parse(applyQuenchable.ToJsonToken()))
                    }
                };

                if (sortedSlots.Length > 2) {
                    MultiPartRenderingHelpers.BuildToolRenderFromAllSeparateParts(craftedItemStack, sortedSlots[0].Itemstack, sortedSlots[1].Itemstack, sortedSlots[2].Itemstack);
                } else {
                    MultiPartRenderingHelpers.BuildToolRenderFromAllSeparateParts(craftedItemStack, sortedSlots[0].Itemstack, sortedSlots[1].Itemstack);
                }

                craftedItemStack.Collectible.ConsumeCraftingIngredients(sortedSlots, placeholderOutput, DummyRecipe);
                craftedItemStack.Collectible.OnCreatedByCrafting(sortedSlots, placeholderOutput, DummyRecipe); //Hopefully call this just like it would if properly crafted in the grid!

                sortedSlots[0].TakeOut(1);
                sortedSlots[0].MarkDirty();
                sortedSlots[1].TakeOut(1); //Decrement inputs, and place the finished item in the ToolHead's Slot
                sortedSlots[1].MarkDirty();
                if (sortedSlots.Length == 3) {
                    if (sortedSlots[2].Itemstack.Block as BlockLiquidContainerBase != null) {
                        var liquidContainer = sortedSlots[2].Itemstack.Block as BlockLiquidContainerBase;
                        var binding = liquidContainer.GetContent(sortedSlots[2].Itemstack);
                        var bindingPart = ToolsmithModSystem.Stats.BindingParts.TryGetValue(binding.Collectible.Code.Path);
                        liquidContainer.TryTakeLiquid(sortedSlots[2].Itemstack, bindingPart.litersUsed);
                        sortedSlots[2].MarkDirty();
                    } else {
                        sortedSlots[2].TakeOut(1);
                        sortedSlots[2].MarkDirty();
                    }
                }

                return craftedItemStack;
            }

            return null;
        }

        //Which part of a tool a stack is, for the workbench slot indicators and for deciding what a slot will accept.
        //None covers both "not a part at all" and "on the ignore list", which callers treat the same way.
        public enum EnumToolPart {
            None,
            Head,
            Handle,
            Binding
        }

        public static EnumToolPart IsAnyToolPart(ItemStack itemstack, IWorldAccessor world) {
            if (world.Side.IsServer() && ToolsmithModSystem.IgnoreCodes.Count > 0 && ToolsmithModSystem.IgnoreCodes.Contains(itemstack.Collectible.Code.ToString())) {
                return EnumToolPart.None;
            } else if (IsValidHead(itemstack)) {
                return EnumToolPart.Head;
            } else if (IsValidHandle(itemstack)) {
                return EnumToolPart.Handle;
            } else if (IsBindingItem(itemstack)) { //Asking what a stack IS, so an absent one must not come back as a binding.
                return EnumToolPart.Binding;
            }

            return EnumToolPart.None;
        }

        //Where a honeable item keeps its durability and sharpness. The three kinds store them under different
        //attributes, so every step of the honing path has to know which it is holding; None means it cannot be honed.
        public enum EnumSharpenTarget {
            None,
            TinkeredTool,
            SmithedTool,
            ToolHead
        }

        //This checks if it is a valid repair tool as well as if it is a fully tinkered tool or if it is just a tool's head, since the durabilities are stored under different attributes
        public static EnumSharpenTarget IsValidSharpenTool(CollectibleObject item, IWorldAccessor world) {
            if (world.Side.IsServer() && ToolsmithModSystem.IgnoreCodes.Count > 0 && ToolsmithModSystem.IgnoreCodes.Contains(item.Code.ToString())) { //First check if the ignore list has any entries, and ensure this one isn't on it. Likely means something got improperly given the Behavior on init.
                return EnumSharpenTarget.None;
            } else if (item.HasBehavior<CollectibleBehaviorTinkeredTools>()) { //This one stores it under 'tinkeredToolHead' durability
                if (!item.HasBehavior<CollectibleBehaviorToolBlunt>()) {
                    return EnumSharpenTarget.TinkeredTool;
                }
            } else if (item.HasBehavior<CollectibleBehaviorSmithedTools>()) { //And this one just uses the regular durability values since it's just a single solid tool, no parts
                if (!item.HasBehavior<CollectibleBehaviorToolBlunt>()) {
                    return EnumSharpenTarget.SmithedTool;
                }
            } else if (item.HasBehavior<CollectibleBehaviorToolHead>()) { //While this stores it as just 'toolPartDurability', since not every part will be a head, but every head will have this behavior
                if (!item.HasBehavior<CollectibleBehaviorToolBlunt>()) {
                    return EnumSharpenTarget.ToolHead;
                }
            }

            return EnumSharpenTarget.None;
        }

        public static bool IsDeconstructableTool(CollectibleObject item, IWorldAccessor world) {
            if (world.Side.IsServer() && ToolsmithModSystem.IgnoreCodes.Count > 0 && ToolsmithModSystem.IgnoreCodes.Contains(item.Code.ToString())) { //First check if the ignore list has any entries, and ensure this one isn't on it. Likely means something got improperly given the Behavior on init.
                return false;
            } else if (item.HasBehavior<CollectibleBehaviorTinkeredTools>()) { //This one stores it under 'tinkeredToolHead' durability
                return true;
            } else if (item is ItemWorkItem) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Whether a tooltip should tell the player this item still has its free first honing to spend.
        /// </summary>
        /// <remarks>
        /// Having never been honed is not on its own worth saying: a head sharpened as a loose part keeps its free
        /// honing, because SetResultsOfSharpening only records a hone value once the free one is spent, and the
        /// craft that fits it to a handle carries over a value that was never written. The result is a tool that is
        /// already sharp and still advertises honing it does not need. Asking whether it is actually dull as well
        /// is what separates the two, and ToolOrHeadNeedsSharpening is the same test the whetstone and grindstone
        /// use to decide when to stop, rather than a second opinion about the same question.
        /// </remarks>
        public static bool ShouldOfferFreeHoning(ItemStack item, IWorldAccessor world) {
            return !item.HasTotalHoneValue() && ToolOrHeadNeedsSharpening(item, world);
        }

        public static bool ToolOrHeadNeedsSharpening(ItemStack item, IWorldAccessor world, EntityAgent byEntity = null) {
            int curSharp;
            int maxSharp;
            int curDur;
            float durPercent;
            var toolType = IsValidSharpenTool(item.Collectible, world);

            if (toolType == EnumSharpenTarget.TinkeredTool) {
                curSharp = item.GetToolCurrentSharpness();
                maxSharp = item.GetToolMaxSharpness();
                curDur = item.GetToolheadCurrentDurability();
                durPercent = item.GetToolheadDurabilityPercent();
            } else if (toolType == EnumSharpenTarget.SmithedTool) {
                curSharp = item.GetToolCurrentSharpness();
                maxSharp = item.GetToolMaxSharpness();
                curDur = item.GetSmithedDurability();
                durPercent = item.GetSmithedRemainingHPPercent();
            } else if (toolType == EnumSharpenTarget.ToolHead) {
                curSharp = item.GetPartCurrentSharpness();
                maxSharp = item.GetPartMaxSharpness();
                curDur = item.GetPartCurrentDurability();
                durPercent = item.GetPartRemainingHPPercent();
            } else {
                return false;
            }

            if (byEntity != null && durPercent <= 0.01f) {
                if (byEntity.Api.Side.IsClient()) {
                    (byEntity.Api as ICoreClientAPI).TriggerIngameError(item, "HoningStopBeforeBreak", Lang.Get("honing-cutoff-message"));
                }
            }

            return (curSharp < maxSharp && curDur > 0 && durPercent > 0.01f);
        }

        public static bool TryWhetstoneSharpening(ref float lastInterval, float secondsUsed, ItemSlot slot, EntityAgent byEntity) {
            if (byEntity.World.Side.IsServer()) {
                var deltaLastTick = secondsUsed - lastInterval;

                if (deltaLastTick >= ToolsmithConstants.SharpenInterval) { //Try not to repair EVERY single tick to space it out some. Cause of this, repair (configurable %) durability each time so it doesn't take forever.
                    var whetstone = WhetstoneInOffhand(byEntity);

                    if (whetstone != null && !slot.Empty) { //If the offhand is still a Whetstone, sharpen! Otherwise break out of this entirely and end the action.
                        var isTool = IsValidSharpenTool(slot.Itemstack.Collectible, byEntity.World);
                        whetstone.HandleSharpenTick(secondsUsed, slot, byEntity.LeftHandItemSlot, byEntity, isTool);
                    } else {
                        return false;
                    }

                    lastInterval = MathUtility.FloorToNearestMult(secondsUsed, ToolsmithConstants.SharpenInterval);

                    if (byEntity.LeftHandItemSlot.Empty || byEntity.LeftHandItemSlot.Itemstack.WhetstoneDoneSharpen()) {
                        return false; //End the interaction when it doesn't need sharpening anymore
                    }
                }
            }

            return true;
        }

        //The next three methods are for the three steps of handling the sharpness honing. It helped to encapsulate it all to handle both the Grindstone and the Whetstones here.
        public static void RecieveDurabilitiesAndSharpness(ref int curDur, ref int maxDur, ref int curSharp, ref int maxSharp, ref float totalHoned, ItemStack item, EnumSharpenTarget isTool) {
            if (isTool == EnumSharpenTarget.TinkeredTool) { //The item is a Tinkered Tool! Use the extensions for the tool's head durability.
                curDur = item.GetToolheadCurrentDurability();
                maxDur = item.GetToolheadMaxDurability();
                if (item.HasPlaceholderHead()) { //If the tool still has no proper head item saved to it, something went wrong and an error should have been printed.
                    return;
                }
                var handleDur = item.GetToolhandleCurrentDurability(); //This is mostly just being called to test that the tools are fully initialized.
                var bindingDur = item.GetToolbindingCurrentDurability(); //^^^
                curSharp = item.GetToolCurrentSharpness();
                maxSharp = item.GetToolMaxSharpness();
            } else if (isTool == EnumSharpenTarget.SmithedTool) { //The item is a Smithed Tool!
                curDur = item.GetSmithedDurability();
                maxDur = item.GetSmithedMaxDurability();
                curSharp = item.GetToolCurrentSharpness();
                maxSharp = item.GetToolMaxSharpness();
            } else { //The item is just a Tool Head, not on a tool put together. Use the extensions for Part Durability.
                curDur = item.GetPartCurrentDurability();
                maxDur = item.GetPartMaxDurability();
                curSharp = item.GetPartCurrentSharpness();
                maxSharp = item.GetPartMaxSharpness();
            }

            if (item.HasTotalHoneValue()) {
                totalHoned = item.GetTotalHoneValue();
            }
        }

        public static void ActualSharpenTick(ref int curDur, ref int curSharp, int maxSharp, ref float totalSharpnessHoned, bool firstHoning, EntityAgent byEntity) {
            if (curSharp < maxSharp && curDur > 0) {
                float percent = 1.0f;
                if (ToolsmithModSystem.Config.GrindstoneSharpenPerTick > 0.0 && ToolsmithModSystem.Config.GrindstoneSharpenPerTick <= 100.0) {
                    percent = ((float)ToolsmithModSystem.Config.GrindstoneSharpenPerTick / 100f);
                }
                int amountSharpened = (int)Math.Ceiling(percent * maxSharp);

                bool damageDurability = true;
                bool doubleDamage = false;
                double damageMultFromLinear = 0.0;

                if (!firstHoning && ToolsmithModSystem.Config.ShouldHoningDamageHead && ToolsmithModSystem.Config.HoningDamageMult > 0) {
                    if (totalSharpnessHoned > 0.5) {
                        damageDurability = byEntity.World.Rand.NextDouble() < 0.05;
                    } else if (totalSharpnessHoned > 0.4 && totalSharpnessHoned <= 0.5) {
                        damageDurability = MathUtility.ShouldDamageFromSharpening(byEntity.World, totalSharpnessHoned);
                    } else if (totalSharpnessHoned > 0.2 && totalSharpnessHoned <= 0.4) {
                        damageMultFromLinear = MathUtility.GetLinearDamageMult(totalSharpnessHoned);
                    } else {
                        doubleDamage = true;
                    }
                } else {
                    damageDurability = false;
                }

                if (damageDurability) {
                    int amountToDamage = (int)(amountSharpened * ToolsmithModSystem.Config.HoningDamageMult);
                    if (doubleDamage) {
                        amountToDamage *= 2;
                    } else if (damageMultFromLinear > 1.0) {
                        amountToDamage = (int)((double)amountToDamage * damageMultFromLinear);
                    }

                    if (curDur - amountToDamage <= 0) {
                        var overflowDamage = Math.Abs(curDur - amountToDamage) + 1;
                        amountToDamage += overflowDamage;

                        if (overflowDamage > 0 && doubleDamage) {
                            overflowDamage /= 2;
                        } else if (damageMultFromLinear > 1.0) {
                            overflowDamage = (int)((double)amountToDamage / damageMultFromLinear);
                        }
                        amountSharpened -= overflowDamage;
                    }

                    curDur -= amountToDamage;
                }

                curSharp += amountSharpened;
                if (curSharp >= maxSharp) {
                    curSharp = maxSharp;
                    totalSharpnessHoned = 1.0f;
                } else {
                    totalSharpnessHoned += percent;
                }
            }
        }

        public static void SetResultsOfSharpening(int curDur, int curSharp, float totalSharpnessHoned, bool firstHoning, ItemStack item, EntityAgent byEntity, ItemSlot mainHandSlot, EnumSharpenTarget isTool) {
            if (isTool == EnumSharpenTarget.TinkeredTool) {
                if (curDur <= 0) {
                    curDur = 1; //Just in case this ever gets here and it's less then 0, just set it to 1 since it shouldn't be breaking tools. But leaving that bit commented out for now, perhaps can configure it as an option later? Eh!
                }
                item.SetToolheadCurrentDurability(curDur);
                item.SetToolCurrentSharpness(curSharp);
                if (!firstHoning) {
                    item.SetTotalHoneValue(totalSharpnessHoned);
                }
                /*if (curDur <= 0) {
                    CollectibleObject toolObj = item.Collectible;
                    HandleBrokenTinkeredTool(byEntity.World, byEntity, mainHandSlot, 0, curSharp, item.GetToolhandleCurrentDurability(), item.GetToolbindingCurrentDurability(), true, false);
                    item.SetBrokeWhileSharpeningFlag();
                    toolObj.DamageItem(byEntity.World, byEntity, mainHandSlot);
                    if (item != null) {
                        item.ClearBrokeWhileSharpeningFlag();
                    }
                }*/
            } else if (isTool == EnumSharpenTarget.SmithedTool) {
                if (curDur <= 0) {
                    curDur = 1;
                }
                item.SetSmithedDurability(curDur);
                item.SetToolCurrentSharpness(curSharp);
                if (!firstHoning) {
                    item.SetTotalHoneValue(totalSharpnessHoned);
                }
                /*if (curDur <= 0) {
                    item.SetSmithedDurability(1);
                    item.SetBrokeWhileSharpeningFlag();
                    item.Collectible.DamageItem(byEntity.World, byEntity, mainHandSlot);
                    if (item != null) {
                        item.ClearBrokeWhileSharpeningFlag();
                    }
                }*/
            } else {
                if (curDur <= 0) {
                    curDur = 1;
                }
                item.SetPartCurrentDurability(curDur);
                item.SetPartCurrentSharpness(curSharp);
                if (!firstHoning) {
                    item.SetTotalHoneValue(totalSharpnessHoned);
                }
                /*if (curDur <= 0) {
                    //Wait... This might be silly and may be hacky but... Can I just make it into an item AND break it right here right now? Lmao
                    CollectibleObject toolToBreakObj; //This might proc other mod's on damage stuff for the head as if it were a real tool.
                    var success = RecipeRegisterModSystem.TinkerToolGridRecipes.TryGetValue(item.Collectible.Code.ToString(), out toolToBreakObj);
                    if (success) {
                        ItemStack toolToBreak = new ItemStack(byEntity.World.GetItem(toolToBreakObj.Code), 1);
                        if (IsDeconstructableTool(toolToBreak.Collectible, byEntity.World)) { //Just to make sure it isn't on the ignore list already, but it should only come back as a Tinkered Tool.
                                             //Initiate that JUST to insure it breaks now! Haha!
                            item = null;
                            mainHandSlot.Itemstack = toolToBreak.Clone();
                            mainHandSlot.Itemstack.SetToolheadCurrentDurability(1);
                            mainHandSlot.Itemstack.SetBrokeWhileSharpeningFlag();
                            mainHandSlot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, mainHandSlot);
                            if (!mainHandSlot.Empty) {
                                mainHandSlot.Itemstack.ClearBrokeWhileSharpeningFlag();
                                mainHandSlot.Itemstack = null;
                            }
                        } else { //Just in case, if all else fails, just destroy the head. But man I hope this works, haha.
                            item = null;
                        }
                    }
                }*/
            }
        }

        public static void HandleBreakdown(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) {
            var inhand = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible;
            if (inhand.HasBehavior<CollectibleBehaviorTinkeredTools>()) {
                DisassembleTool(secondsUsed, world, byPlayer, blockSel);
            } else if (inhand is ItemWorkItem) {
                BreakDownIntoBits(secondsUsed, world, byPlayer, blockSel);
            }
        }

        public static void DisassembleTool(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) {
            //Actually take apart the tool here!
            //Get the parts of the tool from it
            var tool = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;
            var head = tool.GetToolhead();
            var handle = tool.GetToolhandle();
            var binding = tool.GetToolbinding(); //Might be null if there is no binding.

            //Check if the Binding is null, if not, see if it is still at full durability - otherwise don't return it.
            if (binding != null) {
                if (tool.GetToolbindingCurrentDurability() != tool.GetToolbindingMaxDurability()) {
                    binding = null; //If it's null, no binding drop!
                }
            }

            //For both the Head and Handle, set the part durabilities (and sharpness for head!)
            head.SetPartCurrentDurability(tool.GetToolheadCurrentDurability());
            head.SetPartMaxDurability(tool.GetToolheadMaxDurability());
            head.SetPartCurrentSharpness(tool.GetToolCurrentSharpness());
            head.SetPartMaxSharpness(tool.GetToolMaxSharpness());
            if (tool.HasTotalHoneValue()) {
                head.SetTotalHoneValue(tool.GetTotalHoneValue());
            }
            if (world.Api.ModLoader.IsModEnabled("canjewelry")) {
                CanJewelryCompat.CheckAndHandleJewelryStatTransfer(tool, head);
            }
            handle.SetPartCurrentDurability(tool.GetToolhandleCurrentDurability());
            handle.SetPartMaxDurability(tool.GetToolhandleMaxDurability());

            //Return it all to the player, and get rid of the tool.
            bool gaveHead = false;
            bool gaveHandle = false;
            bool gaveBinding = false;
            var player = byPlayer.Entity;

            if (player != null) {
                gaveHead = player.TryGiveItemStack(head);
                IModularPartRenderer handleBehavior = (IModularPartRenderer)handle.Collectible?.CollectibleBehaviors?.FirstOrDefault(b => (b as IModularPartRenderer) != null);
                if (handleBehavior != null) {
                    handleBehavior.ResetRotationAndOffset(handle);
                }
                gaveHandle = player.TryGiveItemStack(handle);
                if (binding != null) {
                    gaveBinding = player.TryGiveItemStack(binding);
                }
            }

            byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack = null;

            //If no room in inventory, drop in world instead.
            if (!gaveHead) {
                player.World.SpawnItemEntity(head, player.Pos.XYZ);
            }
            if (!gaveHandle) {
                IModularPartRenderer handleBehavior = (IModularPartRenderer)handle.Collectible.CollectibleBehaviors.FirstOrDefault(b => (b as IModularPartRenderer) != null);
                if (handleBehavior != null) {
                    handleBehavior.ResetRotationAndOffset(handle);
                }
                player.World.SpawnItemEntity(handle, player.Pos.XYZ);
            }
            if (binding != null && !gaveBinding) {
                player.World.SpawnItemEntity(binding, player.Pos.XYZ);
            }

            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
        }

        public static void BreakDownIntoBits(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) {
            var workpieceStack = byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack;
            var numVoxels = ReforgingUtility.TotalVoxelsInWorkItem(workpieceStack);

            if (numVoxels > 0) {
                var metal = workpieceStack.Collectible.GetMetalMaterial();
                var bitStack = new ItemStack(world.GetItem(new AssetLocation("metalbit-" + metal)));
                var numBits = Math.Truncate(numVoxels / 2.1);
                var remainder = (numVoxels / 2.1) - numBits;

                if (world.Rand.NextDouble() <= remainder) {
                    numBits++;
                }

                bitStack.StackSize = (int)numBits;
                if (!byPlayer.InventoryManager.TryGiveItemstack(bitStack, true)) {
                    world.SpawnItemEntity(bitStack, byPlayer.Entity.Pos.XYZ);
                }
            }

            byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack = null;
            byPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
        }

        public static bool CheckForAndScrubStickBone(ItemStack stack) {
            if (IsStickOrBone(stack)) {
                if ((!stack.HasPartCurrentDurability() && !stack.HasPartMaxDurability()) || !(stack.GetPartCurrentDurability() < stack.GetPartMaxDurability())) {
                    if (!stack.HasHandleGripTag() && !stack.HasHandleTreatmentTag()) {
                        stack.RemoveMultiPartRenderTree();
                        stack.RemovePartRenderTree();
                        stack.RemoveHandleStatTag();
                        stack.RemovePartCurrentDurability();
                        stack.RemovePartMaxDurability();
                    }
                } else {
                    if (stack.HasMultiPartRenderTree()) {
                        var multiPartRenderTree = stack.GetMultiPartRenderTree();
                        if (multiPartRenderTree.Count == 0) {
                            stack.RemoveMultiPartRenderTree();
                        }
                    } else if (stack.HasPartRenderTree()) {
                        var partRenderTree = stack.GetPartRenderTree();
                        if (partRenderTree.Count == 0) {
                            stack.RemovePartRenderTree();
                        }
                    }
                }
                return true;
            }

            return false;
        }

        public static bool IsStickOrBone(ItemStack stack) {
            if (stack.Collectible.Code == ToolsmithConstants.DefaultHandleCode || stack.Collectible.Code == ToolsmithConstants.BoneHandleCode) {
                return true;
            } else {
                return false;
            }
        }
    }
}
