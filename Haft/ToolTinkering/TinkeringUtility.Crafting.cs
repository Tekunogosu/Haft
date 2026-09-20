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
    //Assembling a tool from its parts, and taking one back apart. The two directions live together because they
    //have to agree about what a complete tool is made of - a part the crafter requires and the disassembler forgets
    //is a part the player loses.
    public static partial class TinkeringUtility {
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
