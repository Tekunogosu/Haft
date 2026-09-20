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
    //The sharpness colour gradient and the durability bar beneath an item, plus the tooltip rewriting that goes
    //with them. Purely presentational: nothing here changes what a tool IS, only how it is drawn and described.
    public static partial class TinkeringUtility {
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
            var toolTier = collectible.GetToolTier(inSlot);

            StringHelpers.RemoveTooltipLineStartingWith(tooltip, Lang.Get("item-tooltip-miningspeed"));
            StringHelpers.RemoveTooltipLineStartingWith(tooltip, Lang.Get("Tool Tier: {0}", toolTier));

            if (miningSpeeds == null || miningSpeeds.Count == 0) {
                return;
            }

            //Only the tools whose tier actually gates something carry one - a pickaxe cannot break ore above its
            //tier, an axe cannot fell the hardest wood. A shovel, knife, hoe or scythe digs soil or cuts plants that
            //have no tier gate at all, so vanilla leaves tooltier unset on them and GetToolTier returns 0. Printing
            //that reads as a tool with the worst possible tier rather than as a tool the stat does not apply to,
            //which is why vanilla writes no line there either.
            if (toolTier > 0) {
                tooltip.AppendLine(Lang.Get("hafttooltier", toolTier));
            }

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
    }
}
