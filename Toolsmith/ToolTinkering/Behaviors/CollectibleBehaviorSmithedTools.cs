using Toolsmith.Compat;
using System.Linq;
using System.Text;
using Toolsmith.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;

namespace Toolsmith.ToolTinkering.Behaviors {
    public class CollectibleBehaviorSmithedTools : CollectibleBehavior {



        public CollectibleBehaviorSmithedTools(CollectibleObject collObj) : base(collObj) {

        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo) {
            if (TinkeringUtility.ShouldNotAccessStats(inSlot)) {
                return;
            }

            var curSharp = inSlot.Itemstack.GetToolCurrentSharpness(); //All that needs to be added to the stringbuilder is the Sharpness.
            var maxSharp = inSlot.Itemstack.GetToolMaxSharpness();

            //Built in a copy, since the sharpness line goes in at an index found in the existing text.
            StringBuilder workingDsc = new StringBuilder();
            workingDsc.Append(dsc);
            int startIndex = 0;
            int endIndex = 0;

            StringHelpers.FindTooltipVanillaDurabilityLine(ref startIndex, ref endIndex, workingDsc, world, withDebugInfo);

            if (!inSlot.Itemstack.Collectible.HasBehavior<CollectibleBehaviorToolBlunt>()) {
                if (inSlot.Itemstack.HasTotalHoneValue() && inSlot.Itemstack.GetTotalHoneValue() > 0 && inSlot.Itemstack.GetTotalHoneValue() < 1) {
                    workingDsc.AppendLine(Lang.Get("smithedtoolhoninginprogress"));
                } else if (TinkeringUtility.ShouldOfferFreeHoning(inSlot.Itemstack, world)) {
                    workingDsc.AppendLine(Lang.Get("smithedtoolfreehone"));
                }
                workingDsc.Insert(startIndex, Lang.Get("toolsharpness", StringHelpers.ColorForDurability(curSharp, maxSharp), curSharp, maxSharp) + '\n');
            }

            TinkeringUtility.ReplaceVanillaToolSpeedLines(inSlot, workingDsc);

            dsc.Clear();
            dsc.Append(workingDsc);
        }

        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, IRecipeBase byRecipe, ref EnumHandling bhHandling) {
            ItemStack foundToolInput = null;
            if (allInputslots.Length > 0) {
                foreach (var slot in allInputslots.Where(i => i.Itemstack != null)) {
                    if (slot.Itemstack.Collectible.HasBehavior<CollectibleBehaviorSmithedTools>() && slot.Itemstack.Collectible.Code == outputSlot.Itemstack.Collectible.Code) {
                        foundToolInput = slot.Itemstack.Clone();
                    }
                }
            }

            if (foundToolInput != null) { //A recipe repairing or converting a tool carries the old one's stats across.
                outputSlot.Itemstack.SetSmithedDurability(foundToolInput.GetSmithedDurability());
                outputSlot.Itemstack.SetToolCurrentSharpness(foundToolInput.GetToolCurrentSharpness());
                outputSlot.Itemstack.SetToolMaxSharpness(foundToolInput.GetToolMaxSharpness());
                return;
            }

            var baseDur = outputSlot.Itemstack.Collectible.GetBaseMaxDurability(outputSlot.Itemstack);
            var toolDur = outputSlot.Itemstack.GetSmithedMaxDurability();
            int sharpness = ScientificSmithyCompat.CalculateMaxSharpness(outputSlot.Itemstack, baseDur);
            int startingSharpness = (int)(sharpness * outputSlot.Itemstack.Collectible.StartingSharpnessMult());

            outputSlot.Itemstack.SetSmithedDurability(toolDur);
            outputSlot.Itemstack.SetToolCurrentSharpness(startingSharpness);
            outputSlot.Itemstack.SetToolMaxSharpness(sharpness);

            if (ToolsmithModSystem.Config.DebugMessages) {
                ToolsmithModSystem.Logger.Debug("Tool's durability is: " + baseDur);
                ToolsmithModSystem.Logger.Debug("So the Smithed Durability is: " + toolDur);
                ToolsmithModSystem.Logger.Debug("And the starting Sharpness is: " + startingSharpness);
                ToolsmithModSystem.Logger.Debug("Finally the max Sharpness is: " + sharpness);
                if (allInputslots.Length > 0) {
                    ToolsmithModSystem.Logger.Debug("This tool had input slots! Was it grid crafted?");
                } else {
                    ToolsmithModSystem.Logger.Debug("This tool had no input slots! Was it smithed?");
                }
            }
        }

        //A smithed tool is one solid piece, so only sharpness and whether the tool takes damage at all are decided
        //here; vanilla handles the durability write and the break itself.
        public override void OnDamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, ref int amount, ref EnumHandling bhHandling) {
            if (world.Side.IsServer()) { //If it's a smithed tool, only need to deal with the Sharpness, and any extra "head" damage. Head in this case is just the tool as a whole.
                ItemStack itemStack = itemslot.Itemstack;
                bool isBluntTool = itemslot.Itemstack.Collectible.HasBehavior<CollectibleBehaviorToolBlunt>();
                var currentDur = itemStack.GetSmithedDurability();

                //Sharpness always drops; whether the tool itself takes durability damage depends on how sharp it was.
                bool doDamageTool = false;
                bool doubleToolDamage = false;
                int currentSharpness = itemStack.GetToolCurrentSharpness();
                int maxSharpness = itemStack.GetToolMaxSharpness();
                float sharpnessPer = itemStack.GetToolSharpnessPercent();

                if (maxSharpness <= 1) { //If the Sharpness Max is 1, likely means something got marked improperly. I don't think it could be 1 otherwise?
                    sharpnessPer = 0f; //Set the percent to one as a placeholder to just avoid infinite sharpness.
                }
                if (currentSharpness > 0) {
                    currentSharpness -= amount;
                } else {
                    doubleToolDamage = true;
                }

                if (!isBluntTool) {
                    itemStack.SetToolCurrentSharpness(currentSharpness);
                    itemStack.SetTotalHoneValue(0);
                }

                if (sharpnessPer < 0.98f) {
                    if (!isBluntTool) {
                        doDamageTool = world.Rand.NextDouble() <= ToolsmithModSystem.Config.SharpWear;
                    } else {
                        doDamageTool = world.Rand.NextDouble() <= ToolsmithModSystem.Config.BluntWear;
                    }
                }

                if (doubleToolDamage && doDamageTool) {
                    currentDur -= amount;
                    itemStack.SetSmithedDurability(currentDur);
                }

                if (doDamageTool && amount >= currentDur) {
                    if (world.Api.ModLoader.IsModEnabled("canjewelry")) {
                        CanJewelryCompat.HandleGemDropsForJewelry(byEntity, itemStack);
                    }
                }

                itemslot.MarkDirty();

                if(!doDamageTool) {
                    bhHandling = EnumHandling.PreventDefault; //This should only prevent the default in the case of we don't want to damage the tool. Otherwise vanilla damages it like normal, and handles the breaking.
                }
            } else if (!world.Side.IsServer()) {
                bhHandling = EnumHandling.PreventDefault; //The server owns these values; the client waits for the sync rather than computing its own and desyncing.
            }
        }

        public override int GetMaxDurability(ItemStack itemstack, int durability, ref EnumHandling bhHandling) {
            bhHandling = EnumHandling.PreventDefault;
            return TinkeringUtility.ScaleToHeadDurability(durability);
        }

        public override float GetMiningSpeed(ItemStack itemstack, BlockSelection blockSel, Block block, IPlayer forPlayer, ref EnumHandling bhHandling) {
            bhHandling = EnumHandling.Handled;
            return TinkeringUtility.SharpnessMiningSpeedMultiplier(itemstack);
        }

        //The tooltip asks GetMiningSpeedModifier rather than GetMiningSpeed, so it needs the same answer or a
        //sharpened tool reads as no faster than a dull one.
        public override float GetMiningSpeedModifier(ItemStack itemstack, ref EnumHandling bhHandling) {
            bhHandling = EnumHandling.Handled;
            return GlobalConstants.ToolMiningSpeedModifier * TinkeringUtility.SharpnessMiningSpeedMultiplier(itemstack);
        }
    }
}
