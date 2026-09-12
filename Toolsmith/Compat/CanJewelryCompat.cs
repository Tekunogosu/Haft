using canjewelry.src;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Toolsmith.Compat {

    //Everything Toolsmith knows about CAN Jewelry. That mod encrusts gems into tools and keeps the sockets in an
    //attribute tree on the stack; Toolsmith has to carry that tree across whenever a tool becomes parts or parts
    //become a tool, and hand the gems back when a tool is finished for good.
    //
    //Callers check IsModEnabled("canjewelry") before calling in. The guard stays with them rather than moving in here,
    //because several of those call sites are already inside a wider mod check and a second hidden one would read as
    //though the work were unconditional.
    public static class CanJewelryCompat {

        //Copies the encrusted-gem tree from one stack to another, so a tool taken apart hands its gems to the head and
        //a head built back into a tool carries them along.
        public static void CheckAndHandleJewelryStatTransfer(ItemStack source, ItemStack destination) {
            if (source != null && destination != null && source.Attributes.HasAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING)) {
                ITreeAttribute sourceEncrustedTree = source.Attributes.GetTreeAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING);
                ITreeAttribute destinationEncrustedTree = destination.Attributes.GetOrAddTreeAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING);
                destinationEncrustedTree = sourceEncrustedTree.Clone();
                destination.Attributes[CANJWConstants.ITEM_ENCRUSTED_STRING] = destinationEncrustedTree;
            }
        }

        //This is copied from CAN Jewelry and edited since it uh, doesn't appear to actually work on the Jewelry side? Even with the chance to drop at 1.0, it simply doesn't. I think it's cause it's trying to access SAPI on the client side?
        //Might need updating if Jewelry updates
        public static void HandleGemDropsForJewelry(Entity byEntity, ItemStack itemstack) {
            if (byEntity == null || (byEntity.Api != null && byEntity.Api.Side == EnumAppSide.Client)) {
                return;
            }

            if (itemstack != null && itemstack.Attributes.HasAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING)) {
                Random r = new Random();


                var tree = itemstack.Attributes.GetTreeAttribute(CANJWConstants.ITEM_ENCRUSTED_STRING);
                for (int i = 0; i < tree.GetAsInt(CANJWConstants.SOCKET_ADDED_NUMBER); i++) {
                    if (canjewelry.src.canjewelry.config.chance_gem_drop_on_item_broken == 0 || r.NextDouble() > canjewelry.src.canjewelry.config.chance_gem_drop_on_item_broken) {
                        continue;
                    }

                    ITreeAttribute socketSlot = tree.GetTreeAttribute("slot" + i.ToString());
                    if (socketSlot != null) {
                        int size = socketSlot.GetInt("size");
                        string gemType = socketSlot.GetString("gemtype");
                        string gemSize;
                        switch (size) {
                            case 1:
                                gemSize = "normal";
                                break;
                            case 2:
                                gemSize = "flawless";
                                break;
                            case 3:
                                gemSize = "exquisite";
                                break;
                            default:
                                continue;
                        }

                        string[] buffNames = (socketSlot[CANJWConstants.ENCRUSTABLE_BUFFS_NAMES] as StringArrayAttribute).value;
                        float[] buffValues = (socketSlot[CANJWConstants.ENCRUSTABLE_BUFFS_VALUES] as FloatArrayAttribute).value;
                        ITreeAttribute gemTree = new TreeAttribute();
                        gemTree[CANJWConstants.ENCRUSTABLE_BUFFS_NAMES] = new StringArrayAttribute(buffNames);
                        gemTree[CANJWConstants.ENCRUSTABLE_BUFFS_VALUES] = new FloatArrayAttribute(buffValues);
                        gemTree.SetString(CANJWConstants.CUTTING_TYPE, socketSlot.GetString(CANJWConstants.CUTTING_TYPE));
                        Item currentItem = byEntity.World.GetItem(new AssetLocation("canjewelry:" + "gem-cut-" + gemSize + "-" + gemType));
                        ItemStack newIS = new ItemStack(currentItem, 1);
                        newIS.Attributes[CANJWConstants.CUT_GEM_TREE] = gemTree;
                        byEntity.World.SpawnItemEntity(newIS, byEntity.Pos.XYZ.Clone().Add(0.5f, 0.25f, 0.5f));
                    }
                }
            }
        }
    }
}
