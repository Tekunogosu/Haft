using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Toolsmith.ToolTinkering.Blocks {
    public class WorkbenchInventory : InventoryGeneric {

        public ItemSlot reforgeStagingSlot => slots[0]; //Each slot can only hold 1 item each.
        public ItemSlot craftingSlot1 => slots[1];
        public ItemSlot craftingSlot2 => slots[2];
        public ItemSlot craftingSlot3 => slots[3];
        public ItemSlot craftingSlot4 => slots[4];
        public ItemSlot craftingSlot5 => slots[5];

        public WorkbenchInventory(ICoreAPI api, BlockPos pos = null) : base(6, "ToolsmithWorkbench", pos?.ToString() ?? "-fake", api, OnNewSlot) {
            Pos = pos;
        }

        public ItemSlot? GetSlotFromSelectionID(int slotID) {
            return slotID switch {
                1 => craftingSlot1,
                2 => craftingSlot2,
                3 => craftingSlot3,
                4 => craftingSlot4,
                5 => craftingSlot5,
                7 => reforgeStagingSlot,
                _ => null
            };
        }

        public bool IsSelectSlotEmpty(int slotID) {
            var slot = GetSlotFromSelectionID(slotID);
            if (slot == null) { //An id that maps to no slot holds nothing, so callers must not go on to take an item out of it.
                return true;
            }

            return slot.Empty;
        }

        public bool AllSlotsEmpty() {
            return slots.All(x => x.Empty);
        }

        //Only the crafting slots, and only those holding something. The reforge slot is deliberately excluded: it
        //stages a tool for reforging rather than contributing a part to a craft.
        public ItemSlot[] GetFullCraftingSlots() {
            List<ItemSlot> filled = new List<ItemSlot>();
            for (int i = (int)WorkbenchSlots.CraftingSlot1; i <= (int)WorkbenchSlots.CraftingSlot5; i++) {
                if (!IsSelectSlotEmpty(i)) {
                    filled.Add(GetSlotFromSelectionID(i));
                }
            }

            return filled.ToArray();
        }

        public ItemStack? GetItemFromSlot(int slotID) {
            var slot = GetSlotFromSelectionID(slotID);

            if (slot == null || slot.Empty) {
                return null;
            }

            return slot.TakeOut(1);
        }

        public bool AddItemToSlot(int slotID, ItemSlot fromSlot) {
            var slot = GetSlotFromSelectionID(slotID);

            if (slot == null || fromSlot.Empty) {
                return false;
            }

            slot.Itemstack = fromSlot.TakeOut(1);
            fromSlot.MarkDirty();
            return true;
        }

        public bool AddAdditionalToSlot(int slotID, ItemSlot fromSlot) { //Currently only the Reforging Slot can hold more then one item at a time
            if (slotID == (int)WorkbenchSlots.ReforgeStaging) {
                var slot = GetSlotFromSelectionID(slotID);

                if (slot == null || fromSlot.Itemstack.Collectible.Code != slot.Itemstack.Collectible.Code || slot.MaxSlotStackSize == slot.StackSize) {
                    return false;
                }

                var count = fromSlot.TryPutInto(Api.World, slot, quantity : 1);
                fromSlot.MarkDirty();
                if (count > 0) {
                    return true;
                }
            }

            return false;
        }

        private static ItemSlot OnNewSlot(int id, InventoryGeneric self) {
            if (id == 0) { //Kinda have to hardcode this index, but it's probably fine since it's pretty unlikely I'll be reusing this anywhere?
                return new ItemSlot((WorkbenchInventory)self) {
                    MaxSlotStackSize = 4
                };
            }

            return new ItemSlot((WorkbenchInventory)self) {
                MaxSlotStackSize = 1
            };
        }
    }
}
