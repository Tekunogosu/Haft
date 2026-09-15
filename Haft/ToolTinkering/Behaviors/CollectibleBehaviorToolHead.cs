using Haft.Compat;
using System.Text;
using Haft.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace Haft.ToolTinkering.Behaviors {
    //Marks a collectible as a tool head, and owns the in-hand craft that joins a head to a handle held in the offhand.
    public class CollectibleBehaviorToolHead : CollectibleBehaviorToolPartWithHealth {

        public CollectibleBehaviorToolHead(CollectibleObject collObj) : base(collObj) {

        }

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling) { //Handle the grinding code here as well as the tool itself! Probably can offload the core interaction to a helper utility function?
            var entPlayer = (byEntity as EntityPlayer);

            //If clicking on a ground-storage block, defer to vanilla deposit. Prevents the crafting
            //intercept from racing with BlockEntityGroundStorage's accept path, which was duping
            //heads when the player clicked a partially occupied quadrant with a valid handle in offhand.
            if (blockSel != null && byEntity.World.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityGroundStorage) {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
                return;
            }

            if (TinkeringUtility.ValidHandleInOffhand(byEntity)) { //Check for Handle in Offhand
                handHandling = EnumHandHandling.PreventDefault;
                handling = EnumHandling.PreventSubsequent;
                if (byEntity.World.Side == EnumAppSide.Server) {
                    byEntity.World.PlaySoundAt(new AssetLocation("sounds/player/messycraft.ogg"), byEntity.Pos.X, byEntity.Pos.Y, byEntity.Pos.Z, null, true, 32f, 1f);
                }
                byEntity.StartAnimation("crafting");
                slot.Itemstack.SetPartBeingCrafted();
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handHandling, ref handling);
        }

        public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling) {
            var crafting = slot.Itemstack.PartBeingCrafted();
            if (crafting) {
                handling = EnumHandling.PreventSubsequent;
                return crafting && secondsUsed < HaftConstants.TimeToCraftTinkerTool; //Time for crafting is now a constant variable!
            }

            return base.OnHeldInteractStep(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
        }

        public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling) {
            if (slot.Itemstack.PartBeingCrafted() && secondsUsed >= HaftConstants.TimeToCraftTinkerTool - 0.1) { //If they were crafting, verify that the countdown is up, and if so, craft it (if there still is a valid offhand handle!)
                handling = EnumHandling.PreventDefault;
                slot.Itemstack.ClearPartBeingCrafted();
                if (byEntity.World.Side.IsServer() && TinkeringUtility.ValidHandleInOffhand(byEntity)) {
                    TinkeringUtility.AssemblePartBundle(slot, byEntity, blockSel);
                }
                byEntity.StopAnimation("crafting");
                return;
            }

            byEntity.StopAnimation("crafting");
            slot.Itemstack.ClearPartBeingCrafted();
            base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
        }

        public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handled) {
            if (slot.Itemstack.PartBeingCrafted()) {
                if (HaftModSystem.Config.AccessibilityDisableNeedToHoldClick) {
                    handled = EnumHandling.PreventSubsequent;
                    return false;
                } else if (secondsUsed >= HaftConstants.TimeToCraftTinkerTool - 0.1) {
                    handled = EnumHandling.PreventSubsequent;
                    slot.Itemstack.ClearPartBeingCrafted();
                    if (byEntity.World.Side.IsServer() && TinkeringUtility.ValidHandleInOffhand(byEntity)) {
                        TinkeringUtility.AssemblePartBundle(slot, byEntity, blockSel);
                    }
                }
            }

            byEntity.StopAnimation("crafting");
            return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason, ref handled);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo) {
            dsc.AppendLine(Lang.Get("toolheaddirections"));
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }

        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, IRecipeBase byRecipe, ref EnumHandling bhHandling) { //TODO - This isn't called when a tool head is smithed. Buh! Have to move it elsewhere, or otherwise get this called.
            //This is still possibly important if somehow someone crafts a Tool Head.

            //The tool this head becomes is what carries the real base durability, and it does not exist yet, so the
            //head is given a standard one and recalculated when the tool is crafted.
            int baseDur = HaftConstants.PartDurabilityBase;
            int partDur = TinkeringUtility.ScaleToHeadDurability(baseDur);
            int sharpness = ScientificSmithyCompat.CalculateMaxSharpness(outputSlot.Itemstack, baseDur);
            int startingSharpness = (int)(sharpness * outputSlot.Itemstack.Collectible.StartingSharpnessMult());

            outputSlot.Itemstack.SetPartCurrentDurability(partDur);
            outputSlot.Itemstack.SetPartMaxDurability(partDur);
            outputSlot.Itemstack.SetToolCurrentSharpness(startingSharpness);
            outputSlot.Itemstack.SetToolMaxSharpness(sharpness);

            if (HaftModSystem.Config.DebugMessages) {
                HaftModSystem.Logger.Debug("The starting Sharpness is: " + startingSharpness);
                HaftModSystem.Logger.Debug("Finally the max Sharpness is: " + sharpness);
            }
        }
    }
}
