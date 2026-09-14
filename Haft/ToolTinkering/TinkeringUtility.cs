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
            switch (HaftModSystem.GradientSelection) {
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

        public static int HaftGetItemDamageColor(ItemStack item) {
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

        //The durability bar on a tinkered tool shows whichever part is closest to giving out, since that part is what
        //ends the tool. A loose part shows its own; anything else falls through to vanilla.
        //
        //A tool missing any of the three part attributes has not been initialized yet, so its vanilla durability is
        //the only figure available and is used until something writes the parts.
        public static int FindLowestCurrentDurabilityForBar(ItemStack itemStack) {
            if (itemStack.Collectible.HasBehavior<CollectibleBehaviorTinkeredTools>()) {
                if (!itemStack.HasToolheadCurrentDurability() || !itemStack.HasToolhandleCurrentDurability() || !itemStack.HasToolbindingCurrentDurability()) {
                    return itemStack.Collectible.GetRemainingDurability(itemStack);
                }

                return Math.Min(itemStack.GetToolheadCurrentDurability(), Math.Min(itemStack.GetToolhandleCurrentDurability(), itemStack.GetToolbindingCurrentDurability()));
            } else if (IsValidHead(itemStack) || IsValidHandle(itemStack)) {
                return itemStack.GetPartCurrentDurability();
            }

            return itemStack.Collectible.GetRemainingDurability(itemStack);
        }

        public static int FindLowestMaxDurabilityForBar(ItemStack itemStack) {
            if (itemStack.Collectible.HasBehavior<CollectibleBehaviorTinkeredTools>()) {
                if (!itemStack.HasToolhandleMaxDurability() || !itemStack.HasToolbindingMaxDurability()) {
                    return itemStack.Collectible.GetMaxDurability(itemStack);
                }

                return Math.Min(itemStack.GetToolheadMaxDurability(), Math.Min(itemStack.GetToolhandleMaxDurability(), itemStack.GetToolbindingMaxDurability()));
            } else if (IsValidHead(itemStack) || IsValidHandle(itemStack)) {
                return itemStack.GetPartMaxDurability();
            }

            return itemStack.Collectible.GetMaxDurability(itemStack);
        }

        /// <summary>
        /// Replaces the tool tier and mining speed lines vanilla wrote with ones laid out the way the rest of a
        /// Haft tooltip is: one material per row, in a monospace column, with the speed colored.
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

            tooltip.AppendLine(Lang.Get("hafttooltier", collectible.GetToolTier(inSlot)));

            var speedModifier = collectible.GetMiningSpeedModifier(inSlot.Itemstack);
            bool wroteHeader = false;
            foreach (var materialSpeed in miningSpeeds) {
                var speed = materialSpeed.Value * speedModifier;
                if (speed < 1.1) {
                    continue;
                }

                if (!wroteHeader) {
                    tooltip.AppendLine(Lang.Get("haftminingspeedheader"));
                    wroteHeader = true;
                }
                tooltip.AppendLine(Lang.Get("haftminingspeedrow", Lang.Get(materialSpeed.Key.ToString()).PadRight(10), StringHelpers.ColorForMiningSpeed(speed), speed.ToString("#.#")));
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
        private static ItemStack CraftToolFromParts(IWorldAccessor world, ItemStack head, ItemSlot[] inputSlots, string recipeName, Action<ItemStack> buildRender = null) {
            CollectibleObject craftedTool;
            if (!RecipeRegisterModSystem.TinkerToolGridRecipes.TryGetValue(head.Collectible.Code.ToString(), out craftedTool)) {
                return null;
            }

            ItemStack craftedItemStack = new ItemStack(world.GetItem(craftedTool.Code), 1);
            ItemSlot placeholderOutput = new ItemSlot(new DummyInventory(world.Api));
            placeholderOutput.Itemstack = craftedItemStack;

            TreeAttribute applyQuenchable = new TreeAttribute();
            applyQuenchable.SetBool("applyquenchablebuffs", true);

            GridRecipe dummyRecipe = new() {
                AverageDurability = false,
                Output = new() {
                    ResolvedItemStack = craftedItemStack,
                    RecipeAttributes = new JsonObject(JToken.Parse(applyQuenchable.ToJsonToken()))
                },
                Name = recipeName == null ? null : new AssetLocation(recipeName)
            };

            buildRender?.Invoke(craftedItemStack);

            craftedItemStack.Collectible.ConsumeCraftingIngredients(inputSlots, placeholderOutput, dummyRecipe);
            craftedItemStack.Collectible.OnCreatedByCrafting(inputSlots, placeholderOutput, dummyRecipe);

            return craftedItemStack;
        }

        public static void AssemblePartBundle(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel) {
            ItemStack bundle = new ItemStack(byEntity.World.GetItem(HaftConstants.ToolBundleCode), 1);
            ItemSlot handleSlot = byEntity.LeftHandItemSlot;
            ItemStack head = slot.TakeOut(1);
            ItemStack handle = handleSlot.TakeOut(1); //One handle, not the whole stack: render data written to the stack would apply to every handle in it.

            MultiPartRenderingHelpers.BuildToolRenderFromHeadAndHandle(bundle, head, handle);

            bundle.SetToolhead(head);
            bundle.SetToolhandle(handle); //Set the bundle's head and handle here!

            handleSlot.MarkDirty();
            ItemStack tempHolder = slot.Itemstack;
            slot.Itemstack = bundle; //The bundle takes the head's slot; anything else stacked there is handed back below.
            slot.MarkDirty();
            if (tempHolder != null) {
                if (!byEntity.TryGiveItemStack(tempHolder)) { //Whatever else was stacked in the head's slot goes back to the player, or on the ground.
                    byEntity.World.SpawnItemEntity(tempHolder, byEntity.Pos.XYZ);
                }
            }
        }

        public static void AssembleFullTool(ItemSlot bundleSlot, EntityAgent byEntity, BlockSelection blockSel) {
            ItemStack head = bundleSlot.Itemstack.GetToolhead();
            ItemSlot headSlot = new ItemSlot(new DummyInventory(HaftModSystem.Api)) { Itemstack = head };
            ItemSlot handleSlot = new ItemSlot(new DummyInventory(HaftModSystem.Api)) { Itemstack = bundleSlot.Itemstack.GetToolhandle() };
            ItemSlot bindingSlot = byEntity.LeftHandItemSlot;

            //The binding is optional, and the offhand is where it comes from when assembling in hand.
            ItemSlot[] inputSlots = bindingSlot.Empty
                ? new ItemSlot[] { headSlot, handleSlot }
                : new ItemSlot[] { headSlot, handleSlot, bindingSlot };

            var craftedItemStack = CraftToolFromParts(byEntity.World, head, inputSlots, "haft:inhandtinkertoolcrafting");
            if (craftedItemStack == null) {
                return;
            }

            //The bundle already carries the render tree its head and handle built, so the binding is added onto that
            //rather than the whole tool being rebuilt from parts.
            if (!bundleSlot.Itemstack.HasBundleHasGenericParts()) {
                var successfulBindingAdd = false;
                if (!bindingSlot.Empty) {
                    successfulBindingAdd = MultiPartRenderingHelpers.AddBindingToExistingToolRender(bundleSlot.Itemstack, GetBindingContent(bindingSlot.Itemstack));
                }
                if (!successfulBindingAdd) {
                    var toolType = MultiPartRenderingHelpers.GetToolTypeFromHeadShapePath(head.Item.Shape.Base.Path);
                    if (toolType != null && HaftModSystem.ToolsWithWoodInBindingShapes.Contains(toolType)) {
                        MultiPartRenderingHelpers.AddWoodPartsOfBindingToExistingToolRender(bundleSlot.Itemstack);
                    }
                }
                craftedItemStack.SetMultiPartRenderTree(bundleSlot.Itemstack.GetMultiPartRenderTree());
            }

            ConsumeBindingFromSlot(bindingSlot);
            bundleSlot.Itemstack = craftedItemStack;
            bundleSlot.MarkDirty();
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
                    return Lang.Get("haft:workbench-needs-head");
                case EnumCraftRefusal.NoHandle:
                    return Lang.Get("haft:workbench-needs-handle");
                case EnumCraftRefusal.ExtraItems:
                    return Lang.Get("haft:workbench-extra-items");
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

            //Every part is a loose item here rather than a bundle, so the whole render tree is built from them.
            var craftedItemStack = CraftToolFromParts(world, sortedSlots[0].Itemstack, sortedSlots, null, tool => {
                if (sortedSlots.Length > 2) {
                    MultiPartRenderingHelpers.BuildToolRenderFromAllSeparateParts(tool, sortedSlots[0].Itemstack, sortedSlots[1].Itemstack, sortedSlots[2].Itemstack);
                } else {
                    MultiPartRenderingHelpers.BuildToolRenderFromAllSeparateParts(tool, sortedSlots[0].Itemstack, sortedSlots[1].Itemstack);
                }
            });
            if (craftedItemStack == null) {
                return null;
            }

            sortedSlots[0].TakeOut(1);
            sortedSlots[0].MarkDirty();
            sortedSlots[1].TakeOut(1);
            sortedSlots[1].MarkDirty();
            if (sortedSlots.Length == 3) {
                ConsumeBindingFromSlot(sortedSlots[2]);
            }

            return craftedItemStack;
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
            if (world.Side.IsServer() && HaftModSystem.IgnoreCodes.Count > 0 && HaftModSystem.IgnoreCodes.Contains(itemstack.Collectible.Code.ToString())) {
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
            if (world.Side.IsServer() && HaftModSystem.IgnoreCodes.Count > 0 && HaftModSystem.IgnoreCodes.Contains(item.Code.ToString())) { //First check if the ignore list has any entries, and ensure this one isn't on it. Likely means something got improperly given the Behavior on init.
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
            if (world.Side.IsServer() && HaftModSystem.IgnoreCodes.Count > 0 && HaftModSystem.IgnoreCodes.Contains(item.Code.ToString())) { //First check if the ignore list has any entries, and ensure this one isn't on it. Likely means something got improperly given the Behavior on init.
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

                if (deltaLastTick >= HaftConstants.SharpenInterval) { //Try not to repair EVERY single tick to space it out some. Cause of this, repair (configurable %) durability each time so it doesn't take forever.
                    var whetstone = WhetstoneInOffhand(byEntity);

                    if (whetstone != null && !slot.Empty) { //If the offhand is still a Whetstone, sharpen! Otherwise break out of this entirely and end the action.
                        var isTool = IsValidSharpenTool(slot.Itemstack.Collectible, byEntity.World);
                        whetstone.HandleSharpenTick(secondsUsed, slot, byEntity.LeftHandItemSlot, byEntity, isTool);
                    } else {
                        return false;
                    }

                    lastInterval = MathUtility.FloorToNearestMult(secondsUsed, HaftConstants.SharpenInterval);

                    if (byEntity.LeftHandItemSlot.Empty || byEntity.LeftHandItemSlot.Itemstack.WhetstoneDoneSharpen()) {
                        return false; //End the interaction when it doesn't need sharpening anymore
                    }
                }
            }

            return true;
        }

        //Honing runs in three steps - read the values, apply one tick, write them back - so the grindstone and the
        //whetstone share the arithmetic and differ only in what drives the ticks.
        public static void RecieveDurabilitiesAndSharpness(ref int curDur, ref int maxDur, ref int curSharp, ref int maxSharp, ref float totalHoned, ItemStack item, EnumSharpenTarget isTool) {
            if (isTool == EnumSharpenTarget.TinkeredTool) { //The item is a Tinkered Tool! Use the extensions for the tool's head durability.
                curDur = item.GetToolheadCurrentDurability();
                maxDur = item.GetToolheadMaxDurability();
                if (item.HasPlaceholderHead()) { //If the tool still has no proper head item saved to it, something went wrong and an error should have been printed.
                    return;
                }
                //Read so their getters initialize a tool that has never had its parts written; the values themselves
                //are not wanted here.
                item.GetToolhandleCurrentDurability();
                item.GetToolbindingCurrentDurability();
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
                if (HaftModSystem.Config.GrindstoneSharpenPerTick > 0.0 && HaftModSystem.Config.GrindstoneSharpenPerTick <= 100.0) {
                    percent = ((float)HaftModSystem.Config.GrindstoneSharpenPerTick / 100f);
                }
                int amountSharpened = (int)Math.Ceiling(percent * maxSharp);

                bool damageDurability = true;
                bool doubleDamage = false;
                double damageMultFromLinear = 0.0;

                if (!firstHoning && HaftModSystem.Config.ShouldHoningDamageHead && HaftModSystem.Config.HoningDamageMult > 0) {
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
                    int amountToDamage = (int)(amountSharpened * HaftModSystem.Config.HoningDamageMult);
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

        //Honing never breaks what is being honed: a durability that reaches zero here is floored at 1 instead.
        //The three targets keep the same values under different attributes, which is the only thing that differs.
        //
        //A hone value is recorded only once the free first honing has been spent, so an unset value is what marks a
        //tool as still holding that free hone.
        public static void SetResultsOfSharpening(int curDur, int curSharp, float totalSharpnessHoned, bool firstHoning, ItemStack item, EntityAgent byEntity, ItemSlot mainHandSlot, EnumSharpenTarget isTool) {
            if (curDur <= 0) {
                curDur = 1;
            }

            if (isTool == EnumSharpenTarget.TinkeredTool) {
                item.SetToolheadCurrentDurability(curDur);
                item.SetToolCurrentSharpness(curSharp);
            } else if (isTool == EnumSharpenTarget.SmithedTool) {
                item.SetSmithedDurability(curDur);
                item.SetToolCurrentSharpness(curSharp);
            } else {
                item.SetPartCurrentDurability(curDur);
                item.SetPartCurrentSharpness(curSharp);
            }

            if (!firstHoning) {
                item.SetTotalHoneValue(totalSharpnessHoned);
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
            return stack.Collectible.Code == HaftConstants.DefaultHandleCode || stack.Collectible.Code == HaftConstants.BoneHandleCode;
        }
    }
}
