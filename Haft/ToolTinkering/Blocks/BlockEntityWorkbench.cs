using System;
using System.Collections.Generic;
using System.Linq;
using Haft.Compat;
using Haft.ToolTinkering.Drawbacks;
using Haft.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Haft.ToolTinkering.Blocks {
    public class BlockEntityWorkbench : BlockEntity {

        protected WorkbenchInventory Inventory { get; private set; }
        protected ICoreClientAPI capi;
        protected Dictionary<string, MeshData> WorkbenchItemMeshCache => ObjectCacheUtil.GetOrCreate(Api, HaftConstants.WorkbenchItemRenderingMeshRefs, () => new Dictionary<string, MeshData>());
        private (float x, float y, float z)[] offsetBySlot = { (0f, 0f, 0f), (0.4f, 1f, 0.3f), (0.6f, 1f, 0.6f), (0.8f, 1f, 0.3f), (1.0f, 1f, 0.6f), (1.2f, 1f, 0.3f), (0f, 0f, 0f), (1.65f, 1f, 0.55f) };

        private int craftingHitsCount = 0;

        //Indexed by the same slot ids the selection boxes use, so slot 1 is at index 1 and the unused 0 and 6 are
        //simply never read. Sized to cover every id rather than only the crafting slots, which keeps the lookups
        //below to a bounds check instead of a switch per slot.
        private (float xoff, float yoff, float zoff, int rot)[] craftingSlotWiggle = new (float, float, float, int)[8];
        protected string[] slotHolds = { "empty", "empty", "empty", "empty", "empty", "empty", "empty", "empty" };

        protected Dictionary<string, MeshData> slotMeshes;

        public override void Initialize(ICoreAPI api) {
            base.Initialize(api);
            capi = api as ICoreClientAPI;
            Inventory ??= new WorkbenchInventory(Api, Pos);
            if (capi != null ) {
                slotMeshes = ObjectCacheUtil.TryGet<Dictionary<string, MeshData>>(api, HaftConstants.WorkbenchSlotShapesCache);
                UpdateMeshes();
                //Pre-populate the per-slot mesh cache on every slot change. SlotModified fires from
                //whichever thread mutated the slot, which is always the main thread for player
                //interactions and inbound sync packets - so any TesselateShape work that happens
                //inside UpdateMesh runs against the texture atlas safely. Without this hook the
                //first chunk-tesselation pass after a slot change runs on the tesselation worker
                //thread, hits an uncached texture, and throws "InsertTexture outside main thread".
                Inventory.SlotModified += OnInventorySlotModified;
            }
        }

        private void OnInventorySlotModified(int slotId) {
            UpdateMesh(slotId);
        }

        public bool IsSelectSlotEmpty(int slotID) {
            return Inventory.IsSelectSlotEmpty(slotID);
        }

        public (float x, float y, float z) GetOffsetBySlot(int slotID) {
            return offsetBySlot[slotID];
        }

        //Which way the bench was placed. Every mesh and drop position on this block is built relative to it, so it is
        //read in a dozen places and always the same way.
        protected BlockFacing BenchFacing => BlockFacing.FromCode(Block.LastCodePart());

        //How far to spin a mesh so it faces the same way the bench does, in degrees. The three rotation sites used to
        //spell this out as their own if-chain; they differ only in what they rotate and whether a wiggle is added on.
        protected float BenchYawDegrees() {
            var facing = BenchFacing;
            if (facing.Equals(BlockFacing.EAST)) {
                return 270f;
            } else if (facing.Equals(BlockFacing.WEST)) {
                return 90f;
            } else if (facing.Equals(BlockFacing.SOUTH)) {
                return 180f;
            }

            return 0f;
        }

        //Degrees to radians, the conversion every Rotate call on this block needs.
        protected static float ToRadians(float degrees) {
            return degrees * (MathF.PI / 180);
        }

        //Only the five crafting slots wiggle; every other id answers with no offset, as the switch these replaced did.
        private static bool IsCraftingSlot(int slotID) {
            return slotID >= (int)WorkbenchSlots.CraftingSlot1 && slotID <= (int)WorkbenchSlots.CraftingSlot5;
        }

        protected (float xoff, float yoff, float zoff, int rot) GetSlotsCraftingWiggleFactor(int slotID) {
            if (!IsCraftingSlot(slotID)) {
                return (0f, 0f, 0f, 0);
            }

            return craftingSlotWiggle[slotID];
        }

        protected void SetSlotsCraftingWiggleFactor(int slotID, (float x, float y, float z, int rot) wiggler) {
            if (!IsCraftingSlot(slotID)) {
                return;
            }

            craftingSlotWiggle[slotID] = wiggler;
        }

        public string GetSlotsHoldsString(int slotID) {
            if (!IsCraftingSlot(slotID)) {
                return "";
            }

            return slotHolds[slotID];
        }

        protected void SetSlotsHoldsString(int slotID, string partType) {
            if (!IsCraftingSlot(slotID)) {
                return;
            }

            slotHolds[slotID] = partType;
        }

        public List<int> GetWhatSlotsAreVisible() {
            List<int> visibleSlots = new List<int>();
            var slot3Holding = GetSlotsHoldsString((int)WorkbenchSlots.CraftingSlot3);
            switch (slot3Holding) {
                case "head":
                    visibleSlots.Add((int)WorkbenchSlots.CraftingSlot2);
                    visibleSlots.Add((int)WorkbenchSlots.CraftingSlot3);
                    visibleSlots.Add((int)WorkbenchSlots.CraftingSlot4);
                    return visibleSlots;
                case "other":
                    visibleSlots.Add((int)WorkbenchSlots.CraftingSlot1);
                    visibleSlots.Add((int)WorkbenchSlots.CraftingSlot2);
                    visibleSlots.Add((int)WorkbenchSlots.CraftingSlot3);
                    visibleSlots.Add((int)WorkbenchSlots.CraftingSlot4);
                    visibleSlots.Add((int)WorkbenchSlots.CraftingSlot5);
                    return visibleSlots;
                default:
                    visibleSlots.Add((int)WorkbenchSlots.CraftingSlot3);
                    return visibleSlots;
            }
        }

        protected void ResetCraftingAttempt() {
            craftingHitsCount = 0;
            for (int i = (int)WorkbenchSlots.CraftingSlot1; i <= (int)WorkbenchSlots.CraftingSlot5; i++) {
                craftingSlotWiggle[i] = (0f, 0f, 0f, 0);
            }
        }

        protected void RandomizeWiggles(IWorldAccessor world) {
            var rand = world.Rand;
            for (int i = 1; i < 6; i++) {
                var wiggler = GetSlotsCraftingWiggleFactor(i);
                wiggler.xoff = (float)((rand.NextDouble() * 0.1f) - 0.05);
                wiggler.zoff = (float)((rand.NextDouble() * 0.1f) - 0.05);
                wiggler.rot = rand.Next(-10, 10);
                SetSlotsCraftingWiggleFactor(i, wiggler);
            }
        }

        public bool TryGetOrPutItemOnWorkbench(int slotSelection, ItemSlot mainHandSlot, IPlayer byPlayer, IWorldAccessor world) { //The item is valid for fitting in the slot, see if it is empty and if so, stick one in! Otherwise try and remove the existing item.
            if (slotSelection >= (int)WorkbenchSlots.CraftingSlot1 && slotSelection <= (int)WorkbenchSlots.CraftingSlot5) {
                ResetCraftingAttempt();
            }
            var workbenchSlotSelection = Inventory.GetSlotFromSelectionID(slotSelection);
            if (workbenchSlotSelection != null && !workbenchSlotSelection.Empty) {
                if (!Inventory.AddAdditionalToSlot(slotSelection, mainHandSlot)) {
                    return TryGetItemFromWorkbench(slotSelection, mainHandSlot, byPlayer, world);
                } else {
                    return true;
                }
            }

            if (Inventory.AddItemToSlot(slotSelection, mainHandSlot)) {
                UpdateSlotIndicators(world);
                return true;
            } else {
                return false;
            }
        }

        public bool TryGetItemFromWorkbench(int slotSelection, ItemSlot mainHandslot, IPlayer byPlayer, IWorldAccessor world) { //For when we are only going to see if an item can be popped out of the slot.
            if (slotSelection >= (int)WorkbenchSlots.CraftingSlot1 && slotSelection <= (int)WorkbenchSlots.CraftingSlot5) {
                ResetCraftingAttempt();
            }
            var workbenchSlotSelection = Inventory.GetSlotFromSelectionID(slotSelection);
            if (workbenchSlotSelection != null && workbenchSlotSelection.Empty) {
                return false; //There's no item to get, so return false cause it didn't succeed in dropping anything. Just to check if the Rendering needs an update or not!
            }

            ItemStack gotItem = Inventory.GetItemFromSlot(slotSelection);
            if (gotItem == null) {
                return false;
            }

            if (!byPlayer.InventoryManager.TryGiveItemstack(gotItem, slotNotifyEffect: true)) {
                var ent = world.SpawnItemEntity(gotItem, new Vec3d(byPlayer.Entity.Pos.X, byPlayer.Entity.Pos.Y, byPlayer.Entity.Pos.Z));
                if (slotSelection >= (int)WorkbenchSlots.CraftingSlot1 && slotSelection <= (int)WorkbenchSlots.CraftingSlot5) {
                    UpdateSlotIndicators(world);
                    PopDisabledSlots(world);
                }
                if (ent != null) {
                    return true;
                } else {
                    return false;
                }
            } else {
                if (slotSelection >= (int)WorkbenchSlots.CraftingSlot1 && slotSelection <= (int)WorkbenchSlots.CraftingSlot5) {
                    UpdateSlotIndicators(world);
                    PopDisabledSlots(world);
                }
                return true;
            }
        }

        private void PopDisabledSlots(IWorldAccessor world) {
            var enabledSlots = GetWhatSlotsAreVisible();
            for (int i = 1; i < 6; i++) {
                if (!enabledSlots.Contains(i) && !IsSelectSlotEmpty(i)) {
                    var item = Inventory.GetSlotFromSelectionID(i).TakeOutWhole();
                    DropInFrontOfBench(item, world);
                }
            }
        }

        public bool AttemptToCraft(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) {
            if (craftingHitsCount < HaftConstants.NumHammerStrikesForWorkbenchCraftAction) {
                craftingHitsCount++;
                RandomizeWiggles(world);
                return true;
            } else {
                ResetCraftingAttempt();

                ItemSlot[] craftingSlots = Inventory.GetFullCraftingSlots();

                //Both sides run the same check so the player is told why the hammer did nothing. Only the client can
                //raise the message, and only the server may decide the craft, so the check runs twice rather than
                //the refusal being sent back from the server.
                if (craftingSlots.Length > 0 && !ReforgingUtility.CheckForPossibleMerger(craftingSlots)) {
                    var refusal = TinkeringUtility.WhyCannotCraftTool(craftingSlots);
                    if (refusal != TinkeringUtility.EnumCraftRefusal.None) {
                        if (world.Side.IsClient() && byPlayer is IClientPlayer) {
                            (world.Api as ICoreClientAPI)?.TriggerIngameError(this, "workbenchcraft", TinkeringUtility.GetCraftRefusalMessage(refusal));
                        }
                        return false;
                    }
                }

                if (world.Side.IsClient()) {
                    return true;
                }

                if (craftingSlots.Count() > 1) {
                    if (ReforgingUtility.CheckForPossibleMerger(craftingSlots)) {
                        var combinedStack = ReforgingUtility.MergeDupesAndReturn(craftingSlots);
                        DropInFrontOfBench(combinedStack, world);
                        UpdateSlotIndicators(world);
                        PopDisabledSlots(world);

                        DamageHammerInHand(world, byPlayer);
                        MarkDirty(redrawOnClient: true);
                        return true;
                    }

                    var craftedTool = TinkeringUtility.TryCraftToolFromSlots(craftingSlots, world, blockSel);
                    if (craftedTool != null) {
                        DropInFrontOfBench(craftedTool, world);
                        UpdateSlotIndicators(world);
                        PopDisabledSlots(world);

                        DamageHammerInHand(world, byPlayer);
                        MarkDirty(redrawOnClient: true);
                        return true;
                    }
                }

                return false;
            }
        }

        //Drops onto the bench's working edge - the side the player stands at - rather than into the block. A
        //reforged work item is pushed half a block further out so it does not land on the bench it came off.
        private void DropInFrontOfBench(ItemStack item, IWorldAccessor world, double extraDistance = 0.0) {
            var facing = BenchFacing;
            Vec3d pos;
            if (facing == BlockFacing.WEST) {
                pos = new Vec3d(Pos.X + 0.5, Pos.Y + 1.1, Pos.Z - extraDistance);
            } else if (facing == BlockFacing.EAST) {
                pos = new Vec3d(Pos.X + 0.5, Pos.Y + 1.1, Pos.Z + 1.0 + extraDistance);
            } else if (facing == BlockFacing.SOUTH) {
                pos = new Vec3d(Pos.X - extraDistance, Pos.Y + 1.1, Pos.Z + 0.5);
            } else {
                pos = new Vec3d(Pos.X + 1.0 + extraDistance, Pos.Y + 1.1, Pos.Z + 0.5);
            }

            world.SpawnItemEntity(item, pos);
        }

        //Damages the hammer that drove a bench action, when the player is actually holding one.
        private static void DamageHammerInHand(IWorldAccessor world, IPlayer byPlayer) {
            var heldSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
            if (heldSlot?.Itemstack?.Collectible.Tool == EnumTool.Hammer) {
                heldSlot.Itemstack.Collectible.DamageItem(world, byPlayer.Entity, heldSlot);
            }
        }

        public bool InitiateReforgeAttempt(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) {
            var reforgingSlot = Inventory.GetSlotFromSelectionID((int)WorkbenchSlots.ReforgeStaging);
            if (reforgingSlot == null || reforgingSlot.Empty) {
                return false;
            }

            var percentDamage = ReforgingUtility.GetReforgablePercentDamage(reforgingSlot.Itemstack);
            if (percentDamage > HaftModSystem.Config.PercentDamageForReforge) {
                return false;
            }

            var recipe = ReforgingUtility.TryGetSmithingRecipeFromCache(reforgingSlot.Itemstack, world.Api);
            if (recipe == null) {
                return false;
            }

            if (recipe.Output.Quantity != reforgingSlot.StackSize) {
                return false;
            }

            //Get the metal type from the item in the slot, then generate a work item for that type of metal
            string metal = reforgingSlot.Itemstack.Collectible.GetMetalItem(world.Api);
            if (metal == null) {
                HaftModSystem.Logger.Warning("Could not get a metal or material variant from " + reforgingSlot.Itemstack.Collectible.Code + ". Cannot start a reforge!");
                return false;
            }
            if (HaftModSystem.Config.DebugMessages) {
                HaftModSystem.Logger.Warning("Here we go! What is the metal? " + metal);
            }
            ItemStack workItem = ReforgingUtility.GetWorkItemFromMetalType(world.Api, metal);
            if (workItem == null) {
                HaftModSystem.Logger.Warning("Could not generate a workitem from this metal - " + metal + " | Unable to start a reforge!");
                return false;
            }
            if (HaftModSystem.Config.DebugMessages) {
                HaftModSystem.Logger.Warning("What is the workitem? " + workItem.Collectible.Code);
            }

            if (world.Api.ModLoader.IsModEnabled("canjewelry")) {
                CanJewelryCompat.HandleGemDropsForJewelry(byPlayer.Entity, reforgingSlot.Itemstack);
            }

            if (world.Api.ModLoader.IsModEnabled("scientificsmithy"))
            {
                ScientificSmithyCompat.HandleStressStrainTransfer(workItem, reforgingSlot.Itemstack, world.Api);
            }

            //Generate the complete work item voxel data from the recipe.
            var recipeVoxels = ReforgingUtility.GetVoxelCopyFromRecipe(recipe); //Smithing Plus trims and modifies this array before Serializing it through the Anvil and applying the data to the WorkItem Attributes. It's likely better to follow suit.

            //Calculate the average number of voxels that should be removed from the full piece, then trim off or move around a few voxels to simulate the damage
            var totalVoxels = ReforgingUtility.TotalVoxelsInRecipe(recipe);
            var remainingVoxels = MathUtility.NumberOfVoxelsLeftInReforge(percentDamage, totalVoxels);

            recipeVoxels = ReforgingUtility.DamageWorkpieceForReforge(recipeVoxels, totalVoxels, remainingVoxels, world.Api);

            //Finally, apply any Drawback's additional modifiers to the voxels - But this can come some time after the release, when Drawbacks are more actually implemented.



            //Serialize it and set it to the workitem, and replace the slot with it!
            ReforgingUtility.SerializeVoxelsToWorkPiece(workItem, recipeVoxels);
            ReforgingUtility.SetRecipeIDToWorkPiece(workItem, recipe);
            reforgingSlot.Itemstack = null;
            reforgingSlot.MarkDirty();
            DamageHammerInHand(world, byPlayer);

            DropInFrontOfBench(workItem, world, 0.5);

            MarkDirty(redrawOnClient: true);

            return true;
        }

        public override void OnBlockBroken(IPlayer byPlayer = null) {
            base.OnBlockBroken(byPlayer);

            if (Inventory != null && !Inventory.AllSlotsEmpty()) {
                Inventory.DropAll(Pos.ToVec3d());
            }
        }

        public void UpdateSlotIndicators(IWorldAccessor world) {
            for (int i = 1; i < 6; i++) {
                if (!Inventory.IsSelectSlotEmpty(i)) {
                    var slot = Inventory.GetSlotFromSelectionID(i);
                    if (slot != null) {
                        var whatPart = TinkeringUtility.IsAnyToolPart(slot.Itemstack, world);
                        switch (whatPart) {
                            case TinkeringUtility.EnumToolPart.Head:
                                SetSlotsHoldsString(i, "head");
                                break;
                            case TinkeringUtility.EnumToolPart.Handle:
                                SetSlotsHoldsString(i, "handle");
                                break;
                            case TinkeringUtility.EnumToolPart.Binding:
                                SetSlotsHoldsString(i, "binding");
                                break;
                            default:
                                SetSlotsHoldsString(i, "other"); //Either it's something that hasn't been set in AnyToolPart yet, or it's a Mergable Item
                                break;
                        }
                    }
                } else {
                    SetSlotsHoldsString(i, "empty");
                }
            }
        }

        public string WhatSlotMarkerIndicator(int slotID) {
            switch (slotID) {
                case (int)WorkbenchSlots.CraftingSlot2:
                    if (GetSlotsHoldsString((int)WorkbenchSlots.CraftingSlot3) == "head" && GetSlotsHoldsString(slotID) == "empty") {
                        return "handle";
                    }
                    return "empty";
                case (int)WorkbenchSlots.CraftingSlot3:
                    if (GetSlotsHoldsString(slotID) == "empty") {
                        return "head";
                    }
                    return "empty";
                case (int)WorkbenchSlots.CraftingSlot4:
                    if (GetSlotsHoldsString((int)WorkbenchSlots.CraftingSlot3) == "head" && GetSlotsHoldsString(slotID) == "empty") {
                        return "binding";
                    }
                    return "empty";
                default:
                    return "empty";
            }
        }

        public void UpdateMeshes() {
            if (Api == null || Api.Side.IsServer()) {
                return;
            }
            for (int i = 1; i <= 7; i++) {
                if (i == 6) {
                    continue;
                }
                UpdateMesh(i);
            }
        }

        protected void UpdateMesh(int i) {
            if (Api == null || Api.Side.IsServer()) {
                return;
            }

            ItemSlot slot = Inventory.GetSlotFromSelectionID(i);
            if (i >= (int)WorkbenchSlots.CraftingSlot1 && i <= (int)WorkbenchSlots.CraftingSlot5) {
                if (!slot.Empty) {
                    GetOrCreateFullSlotMesh(slot.Itemstack, i);
                    return;
                } else {
                    if (HaftModSystem.ClientConfig.ShouldRenderWorkbenchSlotMarkers) {
                        GetOrCreateEmptySlotMesh(i);
                    }
                    return;
                }
            } else if (i == (int)WorkbenchSlots.ReforgeStaging) {
                if (!slot.Empty) {
                    GetOrCreateReforgeSlotMesh(slot.Itemstack, i);
                }
                return;
            }
        }

        protected string GetCacheKeyForEmptySlot(int slotIndex) {
            return BenchFacing.Code + "-slot-" + slotIndex + "-" + WhatSlotMarkerIndicator(slotIndex) + "-" + craftingHitsCount + "-empty";
        }

        protected string GetCacheKeyForItem(ItemStack stack, int slotIndex) {
            IContainedMeshSource meshSource = stack.Collectible?.GetCollectibleInterface<IContainedMeshSource>();
            var facing = BenchFacing;
            if (meshSource != null) {
                var slot = new DummySlot(stack);
                return facing.Code + "-slot-" + slotIndex + "-" + WhatSlotMarkerIndicator(slotIndex) + "-" + craftingHitsCount + "-" + meshSource.GetMeshCacheKey(slot);
            }

            return facing.Code + "-slot-" + slotIndex + "-" + WhatSlotMarkerIndicator(slotIndex) + "-" + craftingHitsCount + "-" + stack.Collectible.Code.ToString();
        }

        protected MeshData GetEmptySlotMesh(int slotIndex) {
            string key = GetCacheKeyForEmptySlot(slotIndex);
            WorkbenchItemMeshCache.TryGetValue(key, out var meshData);
            return meshData;
        }

        protected MeshData GetSlotWithItemMesh(ItemStack stack, int slotIndex) {
            string key = GetCacheKeyForItem(stack, slotIndex);
            WorkbenchItemMeshCache.TryGetValue(key, out var meshData);
            return meshData;
        }

        //The cached marker mesh for whatever this slot is currently asking for. False when the cache has no entry,
        //which callers report and then carry on without a marker rather than failing the whole slot.
        protected bool TryGetSlotMarkerMesh(int slotIndex, out MeshData markerMesh) {
            var markerPath = WhatSlotMarkerIndicator(slotIndex) switch {
                "head" => HaftConstants.WorkbenchSlotMarkerHeadPath,
                "handle" => HaftConstants.WorkbenchSlotMarkerHandlePath,
                "binding" => HaftConstants.WorkbenchSlotMarkerBindingPath,
                _ => HaftConstants.WorkbenchSlotMarkerEmptyPath
            };

            if (!slotMeshes.TryGetValue(markerPath, out var cached)) {
                HaftModSystem.Logger.Warning("Could not get the " + WhatSlotMarkerIndicator(slotIndex) + " slot marker from cache. Marker will not render.");
                markerMesh = null;
                return false;
            }

            markerMesh = cached.Clone();
            return true;
        }

        protected MeshData GetOrCreateEmptySlotMesh(int slotIndex) {
            if (capi == null || !TryGetSlotMarkerMesh(slotIndex, out MeshData markerMeshData)) {
                return new MeshData();
            }

            var offset = offsetBySlot[slotIndex];
            markerMeshData.Translate(offset.x, offset.y + 0.01f, offset.z);
            markerMeshData.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, ToRadians(BenchYawDegrees()), 0);

            WorkbenchItemMeshCache[GetCacheKeyForEmptySlot(slotIndex)] = markerMeshData;

            return markerMeshData;
        }

        protected MeshData GetOrCreateFullSlotMesh(ItemStack stack, int slotIndex) {
            if (capi == null) {
                return new MeshData();
            }

            //A cached mesh already has its slot marker baked in, so it is returned as-is rather than going on to build
            //a second marker below.
            MeshData mesh = GetSlotWithItemMesh(stack, slotIndex);
            if (mesh != null) {
                return mesh;
            }

            mesh = GetStackOwnMesh(stack);

            MeshData markerMeshData = null;
            if (HaftModSystem.ClientConfig.ShouldRenderWorkbenchSlotMarkers) {
                TryGetSlotMarkerMesh(slotIndex, out markerMeshData);
            }

            //Nothing to render the stack with. A slot marker is still worth showing on its own, so that is returned
            //where one was built; otherwise the slot renders empty.
            if (mesh == null && !TryTesselateStackMesh(stack, out mesh)) {
                if (markerMeshData != null) {
                    WorkbenchItemMeshCache[GetCacheKeyForItem(stack, slotIndex)] = markerMeshData;
                    return markerMeshData;
                }

                return new MeshData();
            }

            var offset = offsetBySlot[slotIndex];
            var wiggledOffset = offset;
            var wiggleFactor = GetSlotsCraftingWiggleFactor(slotIndex);
            wiggledOffset.x += wiggleFactor.xoff;
            wiggledOffset.z += wiggleFactor.zoff;

            mesh.Scale(new Vec3f(), 0.5f, 0.5f, 0.5f);
            if (TinkeringUtility.IsValidHead(stack)) {
                mesh.Translate(wiggledOffset.x - 0.65f, wiggledOffset.y, wiggledOffset.z + 0.35f);
                mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, (90 + wiggleFactor.rot) * (MathF.PI / 180), 0);
            } else {
                mesh.Translate(wiggledOffset.x - 0.15f, wiggledOffset.y, wiggledOffset.z - 0.15f);
            }
            
            if (markerMeshData != null) {
                markerMeshData.Translate(offset.x, offset.y + 0.01f, offset.z);
            }
            //The item wiggles as it is hammered; the slot marker underneath it does not, so only the mesh adds the
            //wiggle on top of the bench's own facing.
            var yaw = BenchYawDegrees();
            mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, ToRadians(yaw + wiggleFactor.rot), 0);
            if (markerMeshData != null) {
                markerMeshData.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, ToRadians(yaw), 0);
            }

            if (markerMeshData != null) {
                mesh.AddMeshData(markerMeshData);
            }
            string key = GetCacheKeyForItem(stack, slotIndex);
            WorkbenchItemMeshCache[key] = mesh;

            return mesh;
        }

        //Builds a mesh for a stack that has no IContainedMeshSource of its own, by tesselating the shape its item or
        //block type declares and dressing it in that stack's own textures rather than the shape's defaults.
        //
        //Answers false when there is no shape to work from at all - a work item, or a type with no shape base. The two
        //callers want different things in that case, so neither the empty mesh nor the slot marker is decided here.
        protected bool TryTesselateStackMesh(ItemStack stack, out MeshData mesh) {
            mesh = null;

            Shape shape = null;
            if (stack.Class == EnumItemClass.Item) {
                if ((stack.Item as ItemWorkItem) != null) {
                    return false;
                }
                AssetLocation shapeBase = stack.Item?.Shape?.Base;
                if (shapeBase != null) {
                    shape = capi.TesselatorManager.GetCachedShape(shapeBase);
                }
            } else {
                AssetLocation shapeBase = stack.Block?.Shape?.Base;
                if (shapeBase != null) {
                    shape = capi.TesselatorManager.GetCachedShape(shapeBase);
                }
            }

            if (shape == null) {
                return false;
            }

            ShapeTextureSource texSource = new(capi, shape, "For rendering item on a Workbench");
            texSource.textures.Clear();

            if (shape.Textures != null && shape.Textures.Count > 0) {
                foreach ((string texCode, AssetLocation assetLoc) in shape.Textures) { //Go through the shape's textures and populate the texSource with any that the shape already has defined
                    if (stack.Class == EnumItemClass.Item && stack.Item.Textures.TryGetValue(texCode, out CompositeTexture texture)) { //Grab the item's own textures to slap on instead of the shape's base, or just run with the base.
                        texSource.textures[texCode] = texture;
                    } else if (stack.Class == EnumItemClass.Block && stack.Block.Textures.TryGetValue(texCode, out CompositeTexture blockTexture)) {
                        texSource.textures[texCode] = blockTexture;
                    } else {
                        texSource.textures[texCode] = new CompositeTexture(assetLoc);
                    }
                }
            } else if (stack.Item != null && stack.Item.Textures != null && stack.Item.Textures.Count > 0) {
                foreach ((string texCode, CompositeTexture tex) in stack.Item.Textures) {
                    texSource.textures.Add(texCode, tex);
                }
            } else if (stack.Block != null && stack.Block.Textures != null && stack.Block.Textures.Count > 0) {
                foreach ((string texCode, CompositeTexture tex) in stack.Block.Textures) {
                    texSource.textures.Add(texCode, tex);
                }
            }

            capi.Tesselator.TesselateShape("Part on Workbench rendering", shape, out mesh, texSource);
            return true;
        }

        //The mesh a stack builds for itself, for the collectibles that know how. Null when it has no renderer of its
        //own and a shape has to be tesselated for it instead.
        protected MeshData GetStackOwnMesh(ItemStack stack) {
            IContainedMeshSource meshSource = stack.Collectible?.GetCollectibleInterface<IContainedMeshSource>();
            if (meshSource == null) {
                return null;
            }

            return meshSource.GenMesh(new DummySlot(stack), capi.ItemTextureAtlas, Pos);
        }

        //The cached mesh for this slot if there is one, falling back to whatever the stack renders for itself.
        protected MeshData GetExistingStackMesh(ItemStack stack, int slotIndex) {
            return GetSlotWithItemMesh(stack, slotIndex) ?? GetStackOwnMesh(stack);
        }

        protected MeshData GetOrCreateReforgeSlotMesh(ItemStack stack, int slotIndex) {
            if (capi == null) {
                return new MeshData();
            }

            MeshData mesh = GetExistingStackMesh(stack, slotIndex);

            if (mesh == null && !TryTesselateStackMesh(stack, out mesh)) {
                return new MeshData();
            }

            var offset = offsetBySlot[slotIndex];
            var wiggleFactor = GetSlotsCraftingWiggleFactor(slotIndex);
            offset.x += wiggleFactor.xoff;
            offset.z += wiggleFactor.zoff;

            mesh.Scale(new Vec3f(), 0.5f, 0.5f, 0.5f);
            mesh.Translate(offset.x - 0.15f, offset.y, offset.z - 0.15f);
            mesh.Rotate(new Vec3f(0.5f, 0.5f, 0.5f), 0, ToRadians(BenchYawDegrees() + wiggleFactor.rot), 0);

            string key = GetCacheKeyForItem(stack, slotIndex);
            WorkbenchItemMeshCache[key] = mesh;

            return mesh;
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator) {
            //OnTesselation runs on the chunk-tesselation worker thread. Any TesselateShape work
            //inside this method walks through the texture atlas, and the atlas can only be mutated
            //from the main thread. We use cache-only reads here. If a slot's mesh is not cached,
            //we schedule a main-thread UpdateMesh and return empty for this pass; MarkDirty in
            //the scheduled task causes a re-tesselation that picks up the fresh cache entry.
            if (!Inventory.AllSlotsEmpty()) {
                for (int i = 1; i <= 7; i++) {
                    if (i == 6) {
                        continue;
                    }

                    MeshData mesh;
                    ItemSlot slot = Inventory.GetSlotFromSelectionID(i);
                    if (i >= (int)WorkbenchSlots.CraftingSlot1 && i <= (int)WorkbenchSlots.CraftingSlot5) {
                        if (!slot.Empty) {
                            mesh = GetSlotWithItemMesh(slot.Itemstack, i);
                            if (mesh != null) {
                                mesher.AddMeshData(mesh);
                            } else {
                                SchedulePrecacheOnMainThread(i);
                            }
                            continue;
                        } else {
                            if (capi != null && HaftModSystem.ClientConfig.ShouldRenderWorkbenchSlotMarkers) {
                                mesh = GetEmptySlotMesh(i);
                                if (mesh != null) {
                                    mesher.AddMeshData(mesh);
                                } else {
                                    SchedulePrecacheOnMainThread(i);
                                }
                            }
                            continue;
                        }
                    } else if (i == (int)WorkbenchSlots.ReforgeStaging) {
                        if (!slot.Empty) {
                            mesh = GetSlotWithItemMesh(slot.Itemstack, i);
                            if (mesh != null) {
                                mesher.AddMeshData(mesh);
                            } else {
                                SchedulePrecacheOnMainThread(i);
                            }
                            continue;
                        } else {
                            continue;
                        }
                    }
                }
            }

            return base.OnTesselation(mesher, tessThreadTesselator);
        }

        private readonly HashSet<int> pendingSlotPrecaches = new();
        private void SchedulePrecacheOnMainThread(int slotIndex) {
            if (capi == null) return;
            if (pendingSlotPrecaches.Contains(slotIndex)) return;
            pendingSlotPrecaches.Add(slotIndex);
            capi.Event.EnqueueMainThreadTask(() => {
                pendingSlotPrecaches.Remove(slotIndex);
                UpdateMesh(slotIndex);
                MarkDirty(redrawOnClient: true);
            }, "HaftWorkbenchMeshPrecache");
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve) {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            Inventory = new WorkbenchInventory(worldAccessForResolve.Api, Pos);
            Inventory.FromTreeAttributes(tree);
            //The attribute names stay exactly as they were written: they are saved world data, and renaming them
            //would leave every workbench already placed in a world with blank slot markers after a load.
            for (int i = (int)WorkbenchSlots.CraftingSlot1; i <= (int)WorkbenchSlots.CraftingSlot5; i++) {
                slotHolds[i] = tree.GetAsString("slot" + i + "Holds", "empty");
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree) {
            base.ToTreeAttributes(tree);
            Inventory?.ToTreeAttributes(tree);
            for (int i = (int)WorkbenchSlots.CraftingSlot1; i <= (int)WorkbenchSlots.CraftingSlot5; i++) {
                tree.SetString("slot" + i + "Holds", slotHolds[i]);
            }
        }
    }
}
