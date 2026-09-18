using System;
using System.Collections.Generic;
using Haft.Client;
using Haft.Compat;
using Haft.Config;
using Haft.ToolTinkering;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Haft.Utils {
    public static class ItemStackExtensions {
        //One accessor per attribute, rather than attribute names typed at each call site: a misspelling here is a
        //compile error, where a misspelled string would silently read a different attribute. The repetition is what
        //buys that, so these stay one-per-attribute rather than being folded into a generic pair.
        //
        //The Get calls on a tool's parts write a default when the attribute is missing, which is how a tool from an
        //older save or a creative menu ends up with usable stats. Anything asking a question it must not answer by
        //writing - a tooltip, for one - uses the ForData variants instead.

        // -- ItemStack Extensions for the Tool items themselves --
        //Tool Head on full Tool ItemStack cluster
        public static ItemStack GetToolhead(this ItemStack itemStack) { //If there is no tool head returned, lets reset it in here.
            if (!itemStack.Attributes.HasAttribute(HaftAttributes.ToolHead)) {
                itemStack.ResetNullHead(HaftModSystem.Api.World);
            }
            var head = itemStack.Attributes.GetItemstack(HaftAttributes.ToolHead);
            var gotPart = false;
            if (head != null) {
                gotPart = head.ResolveBlockOrItem(HaftModSystem.Api.World);
            }

            if (gotPart) {
                return head.Clone();
            } else {
                HaftModSystem.Logger.Error("Unable to fully get and resolve Toolhead for ItemStack " + itemStack.Collectible.Code + " | Returning a Candle instead to prevent null errors. If a player recieves this, this is likely why!");
                return new ItemStack(HaftModSystem.Api.World.GetItem(new AssetLocation(HaftConstants.FallbackHeadCode)), 1);
            }
        }

        public static ItemStack? GetToolheadForData(this ItemStack itemStack) { //A version of the above call that specifically is to be used in cases where it is not mandatory for it to be set, and resetting it would be bad. It's either not a tool, or this is just for informational purposes.
            if (!itemStack.Attributes.HasAttribute(HaftAttributes.ToolHead)) {
                return null;
            }
            var head = itemStack.Attributes.GetItemstack(HaftAttributes.ToolHead);
            var gotPart = false;
            if (head != null) {
                gotPart = head.ResolveBlockOrItem(HaftModSystem.Api.World);
            }

            if (gotPart) {
                return head.Clone();
            } else {
                HaftModSystem.Logger.Error("Unable to fully get and resolve ToolheadForData for ItemStack " + itemStack.Collectible.Code + " | Returning Null instead, should be handled on the calling side, as it is possible to return null before this.");
                return null;
            }
        }

        public static void SetToolhead(this ItemStack itemStack, ItemStack toolhead) {
            itemStack.Attributes.SetItemstack(HaftAttributes.ToolHead, toolhead.Clone()); //Stores a clone, so later changes to the caller's stack do not reach the tool.
        }

        public static int GetToolheadCurrentDurability(this ItemStack itemStack) { //Stored as the vanilla durability attribute, so other mods reading durability see the head's.
            itemStack.TestForOldToolheadAttributesAndFix();
            if (!itemStack.Attributes.HasAttribute(HaftAttributes.Durability)) {
                itemStack.ResetNullHead(HaftModSystem.Api.World);
            }
            return itemStack.Collectible.GetRemainingDurability(itemStack); //Trying to hook into the vanilla calls for compatability sake
        }

        public static void SetToolheadCurrentDurability(this ItemStack itemStack, int dur) {
            itemStack.Collectible.SetDurability(itemStack, dur);
        }

        public static bool HasToolheadCurrentDurability(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.Durability);
        }

        public static int GetToolheadMaxDurability(this ItemStack itemStack) {
            return itemStack.Collectible.GetMaxDurability(itemStack);
        }

        public static float GetToolheadDurabilityPercent(this ItemStack itemStack) {
            var curDur = itemStack.GetToolheadCurrentDurability();
            var maxDur = itemStack.GetToolheadMaxDurability();
            if (curDur > 0 && maxDur > 0) {
                return ((float)curDur) / ((float)maxDur);
            }

            return 0.0f;
        }

        //Asks whether the head recorded on this tool is the placeholder Candle, and only asks - GetToolheadForData
        //reads the attribute where GetToolhead would reset a missing one, which would have this question writing to
        //the stack it is asking about. No head recorded at all is not a placeholder head: it is a tool that has not
        //been initialized yet, which the callers reach only after something else has already done that.
        public static bool HasPlaceholderHead(this ItemStack itemStack) {
            var head = itemStack.GetToolheadForData();
            return head != null && head.Collectible.Code == HaftConstants.FallbackHeadCode;
        }

        //Migrates a tool that stored its head durability under Haft's own attributes onto the vanilla durability
        //attribute, then drops the old ones. Runs on read, so a tool from an older save migrates when first touched.
        public static void TestForOldToolheadAttributesAndFix(this ItemStack itemStack) {
            if (itemStack.Attributes.HasAttribute(HaftAttributes.ToolHeadCurrentDur)) {
                itemStack.SetToolheadCurrentDurability(itemStack.Attributes.GetInt(HaftAttributes.ToolHeadCurrentDur));
                itemStack.Attributes.RemoveAttribute(HaftAttributes.ToolHeadCurrentDur);
            }
            if (itemStack.Attributes.HasAttribute(HaftAttributes.ToolHeadMaxDur)) {
                itemStack.Attributes.RemoveAttribute(HaftAttributes.ToolHeadMaxDur);
            }
        }

        //Sharpness!
        public static int GetToolCurrentSharpness(this ItemStack itemStack) {
            if (!itemStack.Attributes.HasAttribute(HaftAttributes.ToolSharpnessCurrent)) {
                itemStack.ResetSharpness(HaftModSystem.Api.World);
            }

            return itemStack.Attributes.GetInt(HaftAttributes.ToolSharpnessCurrent);
        }

        public static void SetToolCurrentSharpness(this ItemStack itemStack, int sharp) {
            itemStack.Attributes.SetInt(HaftAttributes.ToolSharpnessCurrent, sharp);
        }

        public static bool HasToolCurrentSharpness(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.ToolSharpnessCurrent);
        }

        public static int GetToolMaxSharpness(this ItemStack itemStack) {
            if (!itemStack.Attributes.HasAttribute(HaftAttributes.ToolSharpnessMax)) {
                itemStack.ResetSharpness(HaftModSystem.Api.World);
            }
            return itemStack.Attributes.GetInt(HaftAttributes.ToolSharpnessMax);
        }

        public static void SetToolMaxSharpness(this ItemStack itemStack, int sharp) {
            itemStack.Attributes.SetInt(HaftAttributes.ToolSharpnessMax, sharp);
        }

        public static bool HasToolMaxSharpness(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.ToolSharpnessMax);
        }

        public static float GetToolSharpnessPercent(this ItemStack itemStack) {
            var currentSharp = itemStack.GetToolCurrentSharpness();
            var maxSharp = itemStack.GetToolMaxSharpness();
            return ((float)currentSharp) / ((float)maxSharp);
        }

        public static void EnsureSharpnessIsNotOverMax(this ItemStack itemStack) {
            if (!itemStack.Attributes.HasAttribute(HaftAttributes.ToolSharpnessCurrent) || !itemStack.Attributes.HasAttribute(HaftAttributes.ToolSharpnessMax)) {
                itemStack.ResetSharpness(HaftModSystem.Api.World);
                return;
            }

            var curSharp = itemStack.GetToolCurrentSharpness();
            var maxSharp = itemStack.GetToolMaxSharpness();
            if (curSharp > maxSharp) {
                itemStack.SetToolCurrentSharpness(maxSharp);
            }
        }

        //Tool Handle on full Tool ItemStack cluster

        public static ItemStack GetToolhandle(this ItemStack itemStack) { //Writes a default handle when none is recorded, as the head accessor does.
            if (!itemStack.Attributes.HasAttribute(HaftAttributes.ToolHandle)) {
                itemStack.ResetNullHandleOrBinding(HaftModSystem.Api.World);
            }
            var handle = itemStack.Attributes.GetItemstack(HaftAttributes.ToolHandle);
            var gotPart = false;
            if (handle != null) {
                gotPart = handle.ResolveBlockOrItem(HaftModSystem.Api.World);
            }

            if (gotPart) {
                return handle.Clone();
            } else {
                HaftModSystem.Logger.Error("Unable to fully get and resolve ToolHandle for ItemStack " + itemStack.Collectible.Code + " | Returning a Stick instead to prevent null errors. If a player recieves this, this is likely why!");
                return new ItemStack(HaftModSystem.Api.World.GetItem(new AssetLocation(HaftConstants.DefaultHandleCode)), 1);
            }
        }

        public static ItemStack? GetToolhandleForData(this ItemStack itemStack) { //A version of the above call that specifically is to be used in cases where it is not mandatory for it to be set, and resetting it would be bad. It's either not a tool, or this is just for informational purposes.
            if (!itemStack.Attributes.HasAttribute(HaftAttributes.ToolHandle)) {
                return null;
            }
            var handle = itemStack.Attributes.GetItemstack(HaftAttributes.ToolHandle);
            var gotPart = false;
            if (handle != null) {
                gotPart = handle.ResolveBlockOrItem(HaftModSystem.Api.World);
            }

            if (gotPart) {
                return handle.Clone();
            } else {
                HaftModSystem.Logger.Error("Unable to fully get and resolve ToolHandleForData for ItemStack " + itemStack.Collectible.Code + " | Returning Null instead, should be handled on the calling side, as it is possible to return null before this.");
                return null;
            }
        }

        public static void SetToolhandle(this ItemStack itemStack, ItemStack toolhandle) {
            itemStack.Attributes.SetItemstack(HaftAttributes.ToolHandle, toolhandle.Clone());
        }

        public static int GetToolhandleCurrentDurability(this ItemStack itemStack) {
            return itemStack.Attributes.GetInt(HaftAttributes.ToolHandleCurrentDur, itemStack.GetToolhandleMaxDurability());
        }

        public static void SetToolhandleCurrentDurability(this ItemStack itemStack, int dur) {
            itemStack.Attributes.SetInt(HaftAttributes.ToolHandleCurrentDur, dur);
        }

        public static bool HasToolhandleCurrentDurability(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.ToolHandleCurrentDur);
        }

        public static int GetToolhandleMaxDurability(this ItemStack itemStack) {
            if (!itemStack.Attributes.HasAttribute(HaftAttributes.ToolHandleMaxDur)) {
                itemStack.ResetNullHandleOrBinding(HaftModSystem.Api.World);
            }
            return itemStack.Attributes.GetInt(HaftAttributes.ToolHandleMaxDur);
        }

        public static void SetToolhandleMaxDurability(this ItemStack itemStack, int dur) {
            itemStack.Attributes.SetInt(HaftAttributes.ToolHandleMaxDur, dur);
        }

        public static bool HasToolhandleMaxDurability(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.ToolHandleMaxDur);
        }

        public static void EnsureHandleIsNotOverMax(this ItemStack itemStack) {
            if (!itemStack.Attributes.HasAttribute(HaftAttributes.ToolHandleCurrentDur) || !itemStack.Attributes.HasAttribute(HaftAttributes.ToolHandleMaxDur)) {
                itemStack.ResetNullHandleOrBinding(HaftModSystem.Api.World);
                return;
            }

            var maxDur = itemStack.GetToolhandleMaxDurability();
            if (itemStack.GetToolhandleCurrentDurability() > maxDur) {
                itemStack.SetToolhandleCurrentDurability(maxDur);
            }
        }

        public static ItemStack CheckForOldHandleAndConvert(ItemStack handle) {
            var oldHandlePath = handle.Collectible.Code.Path.Split('-');
            HaftModSystem.Logger.Warning("OldHandle[0] is " + oldHandlePath[0]);
            if (oldHandlePath[0].Contains(HaftAttributes.OldHandlePrefix)) {
                oldHandlePath[0] = oldHandlePath[0].Remove(0, 3);
            }
            HaftModSystem.Logger.Warning("OldHandle[0] now is " + oldHandlePath[0]);
            if (handle.Collectible?.Code == null || !HaftModSystem.Stats.BaseHandleParts.ContainsKey(handle.Collectible.Code.Path)) {
                ItemStack newHandle = new ItemStack(HaftModSystem.Api.World.GetItem(new AssetLocation("haft:" + oldHandlePath[0])));
                string treatment = null;
                string grip = null;
                foreach (var bit in oldHandlePath) {
                    if (bit == "crudehandle" || bit == "handle" || bit == "carpentedhandle" || bit == "plain" || bit == "none" || bit == "finished") {
                        continue;
                    }

                    if (bit == "fat" || bit == "wax" || bit == "oil") {
                        treatment = bit;
                    } else {
                        grip = bit;
                    }
                }

                if (newHandle.HasPartRenderTree()) {
                    newHandle.Attributes.RemoveAttribute(HaftAttributes.ModularPartDataTree);
                }

                var multiPartTree = newHandle.GetMultiPartRenderTree();
                var handleTree = multiPartTree.GetPartAndTransformRenderTree(HaftAttributes.ModularPartHandleName);
                var renderTree = handleTree.GetPartRenderTree();
                var textureTree = renderTree.GetPartTextureTree();

                if (oldHandlePath[0] == "handle" || oldHandlePath[0] == "carpentedhandle") {
                    HandlePartDefines handleStats = HaftModSystem.Stats.BaseHandleParts.TryGetValue(oldHandlePath[0]);
                    newHandle.SetHandleStatTag(handleStats.handleStatTag);
                    newHandle.SetHandleMaterialTag(HaftConstants.DefaultMaterialStatKey); //The old handles recorded no wood, so give the stat the same oak the texture below already assumes.
                    renderTree.SetPartShapePath(handleStats.handleShapePath);
                    textureTree.SetPartTexturePathFromKey("wood", HaftConstants.HandleWoodTexturePathMinusType + "oak");
                }

                if (grip != null) {
                    var gripTree = multiPartTree.GetPartAndTransformRenderTree(HaftAttributes.ModularPartGripName);
                    var gripRenderTree = gripTree.GetPartRenderTree();
                    var gripTextureTree = gripRenderTree.GetPartTextureTree();
                    var gripStats = HaftModSystem.Stats.GripStats[grip];

                    newHandle.SetHandleGripTag(grip);
                    if (oldHandlePath[0] == "crude") {
                        gripRenderTree.SetPartShapePath("haft:shapes/item/parts/handles/grips/gripfabric-crude");
                    } else {
                        gripRenderTree.SetPartShapePath("haft:shapes/item/parts/handles/grips/gripfabric");
                    }
                    gripTextureTree.SetPartTexturePathFromKey("grip", gripStats.texturePath);
                }

                if (treatment != null) {
                    newHandle.SetHandleTreatmentTag(treatment);
                }

                return newHandle;
            }

            return handle;
        }

        //Tool Binding on full Tool ItemStack cluster
        public static ItemStack? GetToolbinding(this ItemStack itemStack) { //A Null Tool Binding means that no binding was used, this is the only case where it is allowed.
            if (!itemStack.Attributes.HasAttribute(HaftAttributes.ToolBinding)) {
                return null;
            }
            var binding = itemStack.Attributes.GetItemstack(HaftAttributes.ToolBinding);
            var gotPart = false;
            if (binding != null) {
                gotPart = binding.ResolveBlockOrItem(HaftModSystem.Api.World);
            }

            if (gotPart) {
                return binding.Clone();
            } else {
                HaftModSystem.Logger.Error("Unable to fully get and resolve ToolBinding for ItemStack " + itemStack.Collectible.Code + " | Returning Null instead, should be handled on the calling side, as it is possible to return null before this.");
                return null;
            }
        }

        public static void SetToolbinding(this ItemStack itemStack, ItemStack binding) {
            itemStack.Attributes.SetItemstack(HaftAttributes.ToolBinding, binding.Clone());
        }

        public static int GetToolbindingCurrentDurability(this ItemStack itemStack) {
            return itemStack.Attributes.GetInt(HaftAttributes.ToolBindingCurrentDur, itemStack.GetToolbindingMaxDurability());
        }

        public static void SetToolbindingCurrentDurability(this ItemStack itemStack, int dur) {
            itemStack.Attributes.SetInt(HaftAttributes.ToolBindingCurrentDur, dur);
        }

        public static bool HasToolbindingCurrentDurability(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.ToolBindingCurrentDur);
        }

        public static int GetToolbindingMaxDurability(this ItemStack itemStack) {
            if (!itemStack.Attributes.HasAttribute(HaftAttributes.ToolBindingMaxDur)) {
                itemStack.ResetNullHandleOrBinding(HaftModSystem.Api.World);
            }
            return itemStack.Attributes.GetInt(HaftAttributes.ToolBindingMaxDur);
        }

        public static void SetToolbindingMaxDurability(this ItemStack itemStack, int dur) {
            itemStack.Attributes.SetInt(HaftAttributes.ToolBindingMaxDur, dur);
        }

        public static bool HasToolbindingMaxDurability(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.ToolBindingMaxDur);
        }

        public static void EnsureBindingIsNotOverMax(this ItemStack itemStack) {
            if (!itemStack.Attributes.HasAttribute(HaftAttributes.ToolBindingCurrentDur) || !itemStack.Attributes.HasAttribute(HaftAttributes.ToolBindingMaxDur)) {
                return;
            }

            var maxDur = itemStack.GetToolbindingMaxDurability();
            if (itemStack.GetToolbindingCurrentDurability() > maxDur) {
                itemStack.SetToolbindingCurrentDurability(maxDur);
            }
        }

        //Other Full Tool stats inhereted from the parts, set on crafting generally
        public static float GetGripChanceToDamage(this ItemStack itemStack) {
            return itemStack.Attributes.GetFloat(HaftAttributes.GripChanceToDamage, 1.0f);
        }

        public static void SetGripChanceToDamage(this ItemStack itemStack, float chance) {
            itemStack.Attributes.SetFloat(HaftAttributes.GripChanceToDamage, chance);
        }

        public static float GetSpeedBonus(this ItemStack itemStack) {
            return itemStack.Attributes.GetFloat(HaftAttributes.SpeedBonus);
        }

        public static void SetSpeedBonus(this ItemStack itemStack, float speed) {
            itemStack.Attributes.SetFloat(HaftAttributes.SpeedBonus, speed);
        }

        //Extensions for Smithed Tools specifically

        //Intended to be used for Smithed Tools, but this is techincally just looking at the vanilla Durability attribute
        public static int GetSmithedDurability(this ItemStack itemStack) {
            return itemStack.Collectible.GetRemainingDurability(itemStack); //Trying to hook into the vanilla calls for compatability sake
        }

        public static void SetSmithedDurability(this ItemStack itemStack, int dur) {
            itemStack.Collectible.SetDurability(itemStack, dur); //Trying to hook into the vanilla calls for compatability sake
        }

        public static int GetSmithedMaxDurability(this ItemStack itemStack) {
            return itemStack.Collectible.GetMaxDurability(itemStack);
        }

        public static float GetSmithedRemainingHPPercent(this ItemStack itemStack) {
            var currentDur = itemStack.GetSmithedDurability();
            var maxDur = itemStack.GetSmithedMaxDurability();
            if (currentDur > 0 && maxDur > 0) {
                return ((float)currentDur / (float)maxDur);
            }
            return 0.0f;
        }

        //Extensions to handle resetting invalid tools that are lacking any durability values

        //Since it's possible to have issues either detecting proper tools, configuration being wonky, or other things, there needs to be a default fallback to set here.
        //Any time the Tool Head is accessed, check if it's the Dummy and revert to vanilla mechanics to prevent crashing.
        public static void ResetNullHead(this ItemStack itemStack, IWorldAccessor world) {
            if (itemStack.Attributes == null) {
                itemStack.Attributes = new TreeAttribute();
            }

            //Both ways of failing to resolve a head end the same way: say what went wrong, stop treating this
            //collectible as tinkerable, and leave placeholder values behind so the tool reverts to vanilla
            //behaviour instead of erroring again on every access. Only the diagnosis differs, so only the message
            //is passed in - the two callers describe different problems the user has to fix differently.
            void FallBackToVanilla(string reason) {
                HaftModSystem.Logger.Error(reason);
                HaftModSystem.IgnoreCodes.Add(itemStack.Collectible.Code.ToString());
                var headStackBackup = new ItemStack(world.GetItem(new AssetLocation(HaftConstants.FallbackHeadCode)), 1); //Placeholder Candle! It'll be something so it actually _has_ something in there. No more nulls.
                itemStack.SetToolhead(headStackBackup);
                itemStack.SetToolheadCurrentDurability(1); //Set these to just 1 so that something is set. Since this code should be ignored everywhere, this might help prevent re-checking it as well.
                itemStack.SetToolCurrentSharpness(1);
                itemStack.SetToolMaxSharpness(1);
            }

            //Figure out the Tool Head and add the missing stats and ItemStack!
            if (RecipeRegisterModSystem.TinkerToolGridRecipes?.Count > 0) { //If this is being ran on the server-side or it is singleplayer, then RecipeRegisterModSystem will have actually booted and everything!
                string headCode = null;
                foreach (var t in RecipeRegisterModSystem.TinkerToolGridRecipes) {
                    if (t.Value.Code.Equals(itemStack.Collectible.Code)) {
                        headCode = t.Key;
                        break;
                    }
                }

                if (headCode == null) { //If headCode is still null at this point, it never found a proper key. Is something wrong with the configs, or is something getting improperly registered through a wildcard?
                                        //Either way, this needs an error printed and it has to be accounted for at any point.
                    FallBackToVanilla("Ran into a tool without an entry in the GridRecipes Dictionary! Something might be wrong with your configs, or something is getting improperly given the behaviors?\nThe Itemstack in question is: " + itemStack.ToString() + "\nAdding it to the Ignore list to revert to vanilla behaviors when encountered again, and assigning placeholder values to hopefully prevent this from being run again. If you get a Candle when this breaks somehow, this is why!");
                    return;
                }

                if (headCode.Contains("-bone")) {
                    headCode = headCode.Remove(headCode.Length - 5);
                }

                var baseDur = itemStack.Collectible.GetBaseMaxDurability(itemStack);
                var headItem = world.GetItem(new AssetLocation(headCode));
                if (headItem == null) {
                    //Head code resolved from the GridRecipes dictionary but no item with that code is registered
                    //in the current load (mod uninstalled, modid renamed in a takeover, or wildcard resolved
                    //without a backing item). Route through the same fallback the "headCode is null" path above uses.
                    FallBackToVanilla("Tool head code '" + headCode + "' resolved from the GridRecipes Dictionary but no item with that code is registered. The mod that provided it is likely uninstalled or has been renamed. Adding " + itemStack.Collectible.Code + " to the ignore list and assigning placeholder values.");
                    return;
                }
                var headStack = new ItemStack(headItem, 1);
                var headDur = (int)(itemStack.Attributes.GetInt(HaftAttributes.Durability, baseDur) * HaftModSystem.Config.HeadDurabilityMult); //Keeps the damage the tool already carries on the head; the handle and binding get fresh stats.
                var headMaxDur = itemStack.GetToolheadMaxDurability();

                headStack.SetPartCurrentDurability(headDur);
                headStack.SetPartMaxDurability(headMaxDur);

                var curSharpness = itemStack.GetToolCurrentSharpness();
                var maxSharpness = itemStack.GetToolMaxSharpness();

                headStack.SetPartCurrentSharpness(curSharpness);
                headStack.SetPartMaxSharpness(maxSharpness);
                itemStack.SetToolhead(headStack);
                itemStack.SetToolheadCurrentDurability(headDur);
            } else {
                //The recipe dictionary is server-side, so a client reaching here cannot know which head this tool
                //takes. It gets the placeholder and a durability derived from the vanilla one; the real head arrives
                //when the server marks the slot dirty, which it does on every attribute write.
                var baseDur = itemStack.Collectible.GetBaseMaxDurability(itemStack);
                var headStack = new ItemStack(world.GetItem(new AssetLocation(HaftConstants.FallbackHeadCode)), 1);
                var curHeadDur = (int)(itemStack.Attributes.GetInt(HaftAttributes.Durability, baseDur) * HaftModSystem.Config.HeadDurabilityMult); //Keeps whatever damage the tool already had on the head, while the handle and binding get fresh stats.
                itemStack.GetToolCurrentSharpness(); //Read so its getter writes a sharpness onto a tool that has none.

                itemStack.SetToolhead(headStack);
                itemStack.SetToolheadCurrentDurability(curHeadDur);
            }
        }

        public static void ResetSharpness(this ItemStack itemStack, IWorldAccessor world) {
            if (itemStack.Attributes == null) {
                itemStack.Attributes = new TreeAttribute();
            }

            var baseDur = itemStack.Collectible.GetBaseMaxDurability(itemStack);
            int sharpness = ScientificSmithyCompat.CalculateMaxSharpness(itemStack, baseDur);

            var startSharpness = (int)(sharpness * itemStack.Collectible.StartingSharpnessMult());
            itemStack.SetToolCurrentSharpness(startSharpness);
            itemStack.SetToolMaxSharpness(sharpness);
        }

        //Since the handle and binding can be expected to not have something, it makes sense to set them to Stick and 'none' respectively as defaults.
        //The binding will possibly be null, but the handle never should be.
        public static void ResetNullHandleOrBinding(this ItemStack itemStack, IWorldAccessor world) {
            if (itemStack.Attributes == null) {
                itemStack.Attributes = new TreeAttribute();
            }

            ItemStack handle = null;
            int maxHandleDur = itemStack.Attributes.GetInt(HaftAttributes.ToolHandleMaxDur, -1); //Specifically getting this from the attributes directly here to prevent looping the call.
            HandlePartDefines handleWithStats = null;

            ItemStack binding = null;
            int maxBindingDur = itemStack.Attributes.GetInt(HaftAttributes.ToolBindingMaxDur, -1); //Specifically getting this from the attributes directly here to prevent looping the call.
            BindingPartDefines bindingWithStats = null;

            if (maxHandleDur < 0) { //Just need to grab the basic Stick if there is no durability already set.
                string handleCode = HaftConstants.DefaultHandleCode;
                if (itemStack.Collectible.Code.Path.Contains("-bone")) {
                    handleCode = HaftConstants.BoneHandleCode;
                }
                handle = new ItemStack(world.GetItem(new AssetLocation(handleCode)), 1);
                handleWithStats = HaftModSystem.Stats.BaseHandleParts.TryGetValue(HaftConstants.DefaultHandlePartKey);
            } else {
                handle = itemStack.GetToolhandle();
                handleWithStats = HaftModSystem.Stats.BaseHandleParts.TryGetValue(handle.Collectible.Code.Path);
            }
            BindingStatDefines bindingStats;
            if (maxBindingDur < 0) { //Since 'None' is a valid binding, it still needs setting to that, and given the default durability levels! No Itemstack needed though.
                bindingStats = HaftModSystem.Stats.BindingStats.Get(HaftConstants.DefaultBindingPartKey);
            } else {
                binding = itemStack.GetToolbinding();
                bindingWithStats = HaftModSystem.Stats.BindingParts.Get(binding.Collectible.Code.Path);
                bindingStats = HaftModSystem.Stats.BindingStats.Get(bindingWithStats.bindingStatTag);
            }

            var handleStats = HaftModSystem.Stats.BaseHandleStats.Get(handleWithStats.handleStatTag);
            var stats = HaftPartStatsHelpers.ResolveHandleStats(handleStats, handle, bindingStats);

            var handleDur = HaftPartStatsHelpers.CalculateHandleDurability(stats);
            var bindingDur = HaftPartStatsHelpers.CalculateBindingDurability(stats);

            if (maxHandleDur < 0) {
                handle.SetPartCurrentDurability((int)handleDur);
                handle.SetPartMaxDurability((int)handleDur);
                itemStack.SetToolhandle(handle);
                itemStack.SetToolhandleCurrentDurability((int)handleDur);
                itemStack.SetToolhandleMaxDurability((int)handleDur);
            }
            if (maxBindingDur < 0) {
                if (binding != null) {
                    binding.SetPartCurrentDurability((int)bindingDur);
                    binding.SetPartMaxDurability((int) bindingDur);
                    itemStack.SetToolbinding(binding);
                }
                itemStack.SetToolbindingCurrentDurability((int)bindingDur);
                itemStack.SetToolbindingMaxDurability((int)bindingDur);
            }

            itemStack.SetSpeedBonus(HaftPartStatsHelpers.CalculateSpeedBonus(stats));
            itemStack.SetGripChanceToDamage(HaftPartStatsHelpers.CalculateGripChanceToDamage(stats));
        }

        // -- ItemStack Extensions for the Part items --
        public static void SetPartCurrentDurability(this ItemStack itemStack, int durability) {
            itemStack.Attributes.SetInt(HaftAttributes.ToolPartCurrentDur, durability);
        }

        public static int GetPartCurrentDurability(this ItemStack itemStack) {
            return itemStack.Attributes.GetInt(HaftAttributes.ToolPartCurrentDur, itemStack.GetPartMaxDurability());
        }

        public static bool HasPartCurrentDurability(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.ToolPartCurrentDur);
        }

        public static void RemovePartCurrentDurability(this ItemStack itemStack) {
            itemStack.Attributes.RemoveAttribute(HaftAttributes.ToolPartCurrentDur);
        }

        public static void SetPartMaxDurability(this ItemStack itemStack, int durability) {
            itemStack.Attributes.SetInt(HaftAttributes.ToolPartMaxDur, durability);
        }

        //Zero, not a stand-in number, when a part has recorded no maximum. A literal default here read as a real
        //maximum to every caller that divides by it, which is how a head whose true maximum was higher produced a
        //remaining-HP fraction above 1.0 and, multiplied back out, a part stronger than a new one. Callers that can
        //act on missing data ask HasPartMaxDurability first; ResetHeadStats is what repairs a part that has none.
        public static int GetPartMaxDurability(this ItemStack itemStack) {
            return itemStack.Attributes.GetInt(HaftAttributes.ToolPartMaxDur, 0);
        }

        public static bool HasPartMaxDurability(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.ToolPartMaxDur);
        }

        public static void RemovePartMaxDurability(this ItemStack itemStack) {
            itemStack.Attributes.RemoveAttribute(HaftAttributes.ToolPartMaxDur);
        }

        public static float GetPartRemainingHPPercent(this ItemStack itemStack) { //Since by design of wanting to let handles and bindings scale to the Tool's vanilla HP values, this is needed to 'hold' over the damage it sustained during use if it survived.
            var currentDur = itemStack.GetPartCurrentDurability();
            var maxDur = itemStack.GetPartMaxDurability();
            if (currentDur > 0 && maxDur > 0) {
                return ((float)currentDur / (float)maxDur);
            }
            return 0.0f;
        }

        public static void SetPartCurrentSharpness(this ItemStack itemStack, int sharpness) { //Only to be used on the tool heads, this doesn't call the reset, because can only handle that when it has a base durability, and the heads are just a regular item.
            itemStack.Attributes.SetInt(HaftAttributes.ToolSharpnessCurrent, sharpness);
        }

        public static int GetPartCurrentSharpness(this ItemStack itemStack) {
            if (!itemStack.Attributes.HasAttribute(HaftAttributes.ToolSharpnessCurrent)) {
                if (!itemStack.Attributes.HasAttribute(HaftAttributes.ToolPartCurrentDur)) {
                    itemStack.ResetHeadStats();
                } else {
                    itemStack.ResetHeadSharpness();
                }
            }

            return itemStack.Attributes.GetInt(HaftAttributes.ToolSharpnessCurrent);
        }

        public static void SetPartMaxSharpness(this ItemStack itemStack, int sharpness) {
            itemStack.Attributes.SetInt(HaftAttributes.ToolSharpnessMax, sharpness);
        }

        public static int GetPartMaxSharpness(this ItemStack itemStack) {
            if (!itemStack.Attributes.HasAttribute(HaftAttributes.ToolSharpnessMax)) {
                itemStack.ResetHeadSharpness();
            }

            return itemStack.Attributes.GetInt(HaftAttributes.ToolSharpnessMax);
        }

        public static float GetPartRemainingSharpnessPercent(this ItemStack itemStack) {
            var currentSharp = itemStack.GetPartCurrentSharpness();
            var maxSharp = itemStack.GetPartMaxSharpness();
            if (currentSharp >= 0 && maxSharp > 0) {
                return ((float)currentSharp / (float)maxSharp);
            }
            return 0.0f;
        }

        public static void SetPartBeingCrafted(this ItemStack itemStack) {
            if (itemStack.Attributes == null) {
                itemStack.Attributes = new TreeAttribute();
            }
            itemStack.Attributes.SetBool(HaftAttributes.PartBeingCrafted, true);
        }

        public static void ClearPartBeingCrafted(this ItemStack itemStack) {
            itemStack.Attributes?.RemoveAttribute(HaftAttributes.PartBeingCrafted);
        }

        public static bool PartBeingCrafted(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.PartBeingCrafted);
        }

        public static void SetTotalHoneValue(this ItemStack itemStack, float honed) {
            itemStack.Attributes.SetFloat(HaftAttributes.TotalHonedPercentSinceLastUse, honed);
        }

        public static float GetTotalHoneValue(this ItemStack itemStack) {
            return itemStack.Attributes.GetFloat(HaftAttributes.TotalHonedPercentSinceLastUse); //If unset, it means it should get the 'first time honing' bonus of no durability loss.
        }

        public static bool HasTotalHoneValue(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.TotalHonedPercentSinceLastUse);
        }

        public static void SetGrindstoneInUse(this ItemStack itemStack, float lastInterval) {
            if (itemStack.Attributes == null) {
                itemStack.Attributes = new TreeAttribute();
            }
            itemStack.Attributes.SetFloat(HaftAttributes.GrindstoneInUse, lastInterval);
        }

        public static float GetGrindstoneInUse(this ItemStack itemStack) {
            return itemStack.Attributes.GetFloat(HaftAttributes.GrindstoneInUse, 0.0f);
        }

        public static void ClearGrindstoneInUse(this ItemStack itemStack) {
            itemStack.Attributes?.RemoveAttribute(HaftAttributes.GrindstoneInUse);
        }

        public static void SetWhetstoneInUse(this ItemStack itemStack, float lastInterval) {
            if (itemStack.Attributes == null) {
                itemStack.Attributes = new TreeAttribute();
            }
            itemStack.Attributes.SetFloat(HaftAttributes.WhetstoneInUse, lastInterval);
        }

        public static float GetWhetstoneInUse(this ItemStack itemStack) {
            return itemStack.Attributes.GetFloat(HaftAttributes.WhetstoneInUse);
        }

        public static void ClearWhetstoneInUse(this ItemStack itemStack) {
            itemStack.Attributes?.RemoveAttribute(HaftAttributes.WhetstoneInUse);
        }

        public static bool WhetstoneInUse(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.WhetstoneInUse);
        }

        public static void SetWhetstoneDoneSharpen(this ItemStack itemStack) {
            if (itemStack.Attributes == null) {
                itemStack.Attributes = new TreeAttribute();
            }
            itemStack.Attributes.SetBool(HaftAttributes.WhetstoneDoneSharpen, true);
        }

        public static void ClearWhetstoneDoneSharpen(this ItemStack itemStack) {
            itemStack.Attributes?.RemoveAttribute(HaftAttributes.WhetstoneDoneSharpen);
        }

        public static bool WhetstoneDoneSharpen(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.WhetstoneDoneSharpen);
        }

        public static void ResetHeadStats(this ItemStack itemStack) {
            if (itemStack.Attributes == null) {
                itemStack.Attributes = new TreeAttribute();
            }

            int maxDur;
            int maxSharp;

            if (RecipeRegisterModSystem.TinkerToolGridRecipes?.Count > 0) {
                CollectibleObject firstTool;
                var success = RecipeRegisterModSystem.TinkerToolGridRecipes.TryGetValue(itemStack.Collectible.Code, out firstTool);
                if (success) {
                    var baseDur = firstTool.GetBaseMaxDurability(new ItemStack(firstTool));
                    maxDur = (int)(baseDur * HaftModSystem.Config.HeadDurabilityMult);

                    itemStack.SetPartMaxDurability(maxDur);
                    itemStack.SetPartCurrentDurability(maxDur);

                    maxSharp = ScientificSmithyCompat.CalculateMaxSharpness(itemStack, baseDur);
                    itemStack.SetPartMaxSharpness(maxSharp);
                    itemStack.SetPartCurrentSharpness((int)(itemStack.Collectible.StartingSharpnessMult() * maxSharp));
                    return;
                }
            }

            //No recipe to read a tool's durability from, so the head is rated from the metal it is made of - the same
            //number it would get if it were crafted now. A head with no entry in the material table keeps whatever it
            //already recorded, and the flat base only stands in for one that has recorded nothing at all.
            var resetMaterialStats = itemStack.GetHeadMaterialStats();
            if (resetMaterialStats != null) {
                maxDur = TinkeringUtility.ScaleToHeadDurability((int)HaftPartStatsHelpers.CalculateHeadDurability(resetMaterialStats));
            } else {
                maxDur = itemStack.HasPartMaxDurability()
                    ? itemStack.GetPartMaxDurability()
                    : TinkeringUtility.ScaleToHeadDurability(HaftConstants.PartDurabilityBase);
            }
            itemStack.SetPartMaxDurability(maxDur);
            itemStack.SetPartCurrentDurability(maxDur);

            maxSharp = ScientificSmithyCompat.CalculateMaxSharpness(itemStack, (int)(maxDur / HaftModSystem.Config.HeadDurabilityMult));
            itemStack.SetPartMaxSharpness(maxSharp);
            itemStack.SetPartCurrentSharpness((int)(itemStack.Collectible.StartingSharpnessMult() * maxSharp));
        }

        public static void ResetHeadSharpness(this ItemStack itemStack) {
            var maxDur = itemStack.GetPartMaxDurability();
            int maxSharp = ScientificSmithyCompat.CalculateMaxSharpness(itemStack, (int)(maxDur / HaftModSystem.Config.HeadDurabilityMult));

            itemStack.SetPartMaxSharpness(maxSharp);
            itemStack.SetPartCurrentSharpness((int)(itemStack.Collectible.StartingSharpnessMult() * maxSharp));
        }

        public static void SetHandleStatTag(this ItemStack itemStack, string tag) {
            itemStack.Attributes.SetString(HaftAttributes.HandleStatTag, tag);
        }

        public static string GetHandleStatTag(this ItemStack itemStack) {
            return itemStack.Attributes.GetString(HaftAttributes.HandleStatTag);
        }

        public static bool HasHandleStatTag(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.HandleStatTag);
        }

        public static void RemoveHandleStatTag(this ItemStack itemStack) {
            itemStack.Attributes.RemoveAttribute(HaftAttributes.HandleStatTag);
        }

        //Everything a handle offers to a part being applied to it: the tags its own part define carries, plus the
        //tags of whatever it is made of. Gathering both is what lets a rule be written against the material without
        //splitting the handle parts per material - one "handle" part still covers all thirteen woods, and an oak
        //handle and a pine handle answer differently because their materials do.
        public static HashSet<string> GetHandleProvidedTags(this ItemStack handle) {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (handle?.Collectible?.Code != null) {
                var part = HaftModSystem.Stats.BaseHandleParts.TryGetValue(handle.Collectible.Code.Path);
                if (part?.providesTags != null) {
                    tags.AddRange(part.providesTags);
                }
            }

            var materialStats = handle.GetHandleMaterialStats();
            if (materialStats?.providesTags != null) {
                tags.AddRange(materialStats.providesTags);
            }

            //A finish the handle already carries is itself something a later treatment can require. Bluing is the
            //case that needs it: the oxide layer is porous on its own and is traditionally sealed with oil, so oil
            //asks for a blued surface rather than for bare metal.
            if (handle != null && handle.HasHandleTreatmentTag()) {
                tags.Add(handle.GetHandleTreatmentTag());
            }

            return tags;
        }

        //Resolves the material stats for a handle, in one place, because two callers need the same answer: the
        //material the stack records, else oak - the fallback a stick, a bone or a crude handle has always taken,
        //having no material of its own, and the one a handle that never passed through crafting takes as well.
        public static MaterialStatDefines GetHandleMaterialStats(this ItemStack handle) {
            if (handle.HasHandleMaterialTag()) {
                var tag = handle.GetHandleMaterialTag();
                if (tag != null && HaftModSystem.Stats.MaterialStats.ContainsKey(tag)) {
                    return HaftModSystem.Stats.MaterialStats.Get(tag);
                }
            }

            return HaftModSystem.Stats.MaterialStats.Get(HaftConstants.DefaultMaterialStatKey);
        }

        //The head counterpart to GetHandleMaterialStats. A head carries no material tag of its own: the metal it was
        //smithed from is already in its variant, so that is what gets looked up rather than a second attribute that
        //could disagree with it.
        //
        //Returns null rather than falling back to oak, which is the opposite of the handle's rule above and
        //deliberate. A stick genuinely has no material and oak stands in for its numbers; a bone or flint head is not
        //a soft metal, and rating it as one would hand it a durability derived from a table it does not belong to.
        //Null lets the caller keep the vanilla-derived durability such a head already has.
        public static MaterialStatDefines GetHeadMaterialStats(this ItemStack head) {
            var metal = head?.Collectible?.GetMetalMaterial();
            if (metal != null && HaftModSystem.Stats.MaterialStats.ContainsKey(metal)) {
                return HaftModSystem.Stats.MaterialStats.Get(metal);
            }

            return null;
        }

        public static void SetHandleMaterialTag(this ItemStack itemStack, string tag) {
            itemStack.Attributes.SetString(HaftAttributes.HandleMaterialTag, tag);
        }

        //Reads the material, migrating a handle saved under the old toolHandleWoodTag name as it goes. The migration
        //lives here rather than in a separate pass so that every caller gets it without having to remember to ask:
        //a handle from an older save is rewritten the first time anything looks at it, and a handle that has already
        //been migrated costs one HasAttribute check.
        public static string GetHandleMaterialTag(this ItemStack itemStack) {
            if (itemStack.Attributes.HasAttribute(HaftAttributes.HandleMaterialTag)) {
                return itemStack.Attributes.GetString(HaftAttributes.HandleMaterialTag);
            }

            if (itemStack.Attributes.HasAttribute(HaftAttributes.LegacyHandleWoodTag)) {
                var legacy = itemStack.Attributes.GetString(HaftAttributes.LegacyHandleWoodTag);
                itemStack.Attributes.SetString(HaftAttributes.HandleMaterialTag, legacy);
                itemStack.Attributes.RemoveAttribute(HaftAttributes.LegacyHandleWoodTag);
                return legacy;
            }

            return null;
        }

        public static bool HasHandleMaterialTag(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.HandleMaterialTag) ||
                   itemStack.Attributes.HasAttribute(HaftAttributes.LegacyHandleWoodTag);
        }

        public static void RemoveHandleMaterialTag(this ItemStack itemStack) {
            itemStack.Attributes.RemoveAttribute(HaftAttributes.HandleMaterialTag);
            itemStack.Attributes.RemoveAttribute(HaftAttributes.LegacyHandleWoodTag);
        }

        public static void SetLimbMaterialTag(this ItemStack itemStack, string tag) {
            itemStack.Attributes.SetString(HaftAttributes.LimbMaterialTag, tag);
        }

        public static string GetLimbMaterialTag(this ItemStack itemStack) {
            return itemStack.Attributes.GetString(HaftAttributes.LimbMaterialTag);
        }

        public static bool HasLimbMaterialTag(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.LimbMaterialTag);
        }

        //How well the bow was built, read off the item code. Null for anything that is not a parted bow, which is
        //every vanilla bow and every unrelated projectile the shooting patches also see.
        public static BowStatDefines GetBowTierStats(this ItemStack bow) {
            var variant = bow?.Collectible?.Variant?["type"];
            if (variant == null || !HaftConstants.BowStatKeyByVariant.TryGetValue(variant, out var tierKey)) {
                return null;
            }

            return HaftModSystem.Stats.BowStats.Get(tierKey);
        }

        //Whether this bow records the wood it was built from. A crude bow is lashed together from sticks and cordage,
        //so there is no species to name and none is asked for - the same reason a crude handle has no wood variant.
        //A bow whose tier is unknown is treated as having a wood axis, because the tiers that do not are the named
        //exception rather than the default.
        public static bool BowHasWoodAxis(this ItemStack bow) {
            return bow.GetBowTierStats()?.hasWoodAxis != false;
        }

        //The limb counterpart to GetHandleMaterialStats. It shares the oak fallback only where the crude handle
        //already does - a tier with no wood axis has no species to record, so oak stands in for its numbers exactly
        //as it does for a stick, a bone or a crude handle.
        //
        //Everywhere else the fallback is deliberately NOT taken: a limb with no material on a tier that should have
        //one is a bow that was never crafted from a stave, and rating it as oak would quietly hand a creative-spawned
        //bow a real draw weight. Returning null lets the caller say "unknown" instead, the way the handle tooltip
        //already does.
        public static MaterialStatDefines GetLimbMaterialStats(this ItemStack limb) {
            if (!limb.HasLimbMaterialTag()) {
                return limb.BowHasWoodAxis() ? null : HaftModSystem.Stats.MaterialStats.Get(HaftConstants.DefaultMaterialStatKey);
            }

            var tag = limb.GetLimbMaterialTag();
            return tag != null ? HaftModSystem.Stats.MaterialStats.Get(tag) : null;
        }

        //The grip stats for a bow, or null when it has none. A bow with no grip recorded is a bare riser rather than
        //a missing value, so the caller reads null as "contributes nothing" rather than falling back to a default.
        public static GripStatDefines GetBowGripStats(this ItemStack bow) {
            if (bow == null || !bow.HasHandleGripTag()) {
                return null;
            }

            return HaftModSystem.Stats.GripStats.Get(bow.GetHandleGripTag());
        }

        public static void SetHandleGripTag(this ItemStack itemStack, string tag) {
            itemStack.Attributes.SetString(HaftAttributes.HandleGripTag, tag);
        }

        public static string GetHandleGripTag(this ItemStack itemStack) {
            return itemStack.Attributes.GetString(HaftAttributes.HandleGripTag);
        }

        public static bool HasHandleGripTag(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.HandleGripTag);
        }

        public static void SetHandleTreatmentTag(this ItemStack itemStack, string tag) {
            itemStack.Attributes.SetString(HaftAttributes.HandleTreatmentTag, tag);
        }

        public static string GetHandleTreatmentTag(this ItemStack itemStack) {
            return itemStack.Attributes.GetString(HaftAttributes.HandleTreatmentTag);
        }

        public static bool HasHandleTreatmentTag(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.HandleTreatmentTag);
        }

        public static void SetGripAdhesiveTag(this ItemStack itemStack, string tag) {
            itemStack.Attributes.SetString(HaftAttributes.GripAdhesiveTag, tag);
        }

        public static string GetGripAdhesiveTag(this ItemStack itemStack) {
            return itemStack.Attributes.GetString(HaftAttributes.GripAdhesiveTag);
        }

        public static bool HasGripAdhesiveTag(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.GripAdhesiveTag);
        }

        //What a grip offers a handle it is being attached to: its part define's tags, plus "adhesive-backed" when it
        //has actually been backed with one. The adhesive is a stack attribute rather than a part, so a rule about it
        //has to be answered from the stack - reading only the part define would miss it entirely.
        public static HashSet<string> GetGripProvidedTags(this ItemStack grip) {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (grip?.Collectible?.Code != null) {
                var part = HaftModSystem.Stats.GripParts.TryGetValue(grip.Collectible.Code.Path);
                if (part?.providesTags != null) {
                    tags.AddRange(part.providesTags);
                }
            }

            if (grip.HasGripAdhesiveTag()) {
                tags.Add(HaftConstants.AdhesiveBackedTag);
            }

            return tags;
        }

        public static void SetReadyToBlue(this ItemStack itemStack) {
            itemStack.Attributes.SetBool(HaftAttributes.PartReadyToBlue, true);
        }

        public static bool IsReadyToBlue(this ItemStack itemStack) {
            return itemStack.Attributes.GetBool(HaftAttributes.PartReadyToBlue, false);
        }

        public static void SetWetTreatment(this ItemStack itemStack, int hours) {
            itemStack.Attributes.SetInt(HaftAttributes.PartWetTreatment, hours);
        }

        public static int GetWetTreatment(this ItemStack itemStack) {
            return itemStack.Attributes.GetInt(HaftAttributes.PartWetTreatment);
        }

        public static bool HasWetTreatment(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.PartWetTreatment);
        }

        public static void RemoveWetTreatment(this ItemStack itemStack) {
            itemStack.Attributes.RemoveAttribute(HaftAttributes.PartWetTreatment);
            itemStack.Attributes.RemoveAttribute(HaftAttributes.TransitionState);

            var multiPartTree = itemStack.GetMultiPartRenderTree();
            var partAndTransformTree = multiPartTree.GetPartAndTransformRenderTree(HaftAttributes.ModularPartHandleName);
            var renderTree = partAndTransformTree.GetPartRenderTree();
            var textureTree = renderTree.GetPartTextureTree();
            foreach (var texture in textureTree) {
                if (texture.Key.Contains("-overlay")) {
                    textureTree.RemoveAttribute(texture.Key);
                }
            }
        }

        public static void SetDisposeMeNowPlease(this ItemStack itemStack) {
            if (itemStack.Attributes == null) {
                itemStack.Attributes = new TreeAttribute();
            }
            itemStack.Attributes.SetBool(HaftAttributes.DisposeMeNowPleaseTag, true);
        }

        public static bool HasDisposeMeNowPlease(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.DisposeMeNowPleaseTag);
        }

        // -- More Generic ItemStack extensions or helper methods intended to handle items/collectibleobjects --
        public static void AddBehavior<T>(this CollectibleObject collectibleObject) where T : CollectibleBehavior {
            if (collectibleObject == null || collectibleObject.HasBehavior<T>()) {
                return;
            }

            try {
                var addedBehavior = (T)Activator.CreateInstance(typeof(T), collectibleObject);
                collectibleObject.CollectibleBehaviors = collectibleObject.CollectibleBehaviors.Append(addedBehavior);
            } catch (Exception ex) {
                HaftModSystem.Logger.Error("Something went wrong attempting to add a behavior to the provided Collectable with code: " + collectibleObject.Code + "\nIf this isn't an intended Tool or Part, try adding it to the blacklist to avoid this in the future!");
                HaftModSystem.Logger.Error(ex);
            }
        }

        //How sharp a freshly made edge starts out, as a share of its maximum. Metal takes and holds a keener edge
        //than bone, flint or obsidian, so the two are banded rather than sharing one figure.
        public static float StartingSharpnessMult(this CollectibleObject collectibleObject) {
            return collectibleObject.IsCraftableMetal() ? HaftConstants.StartingSharpnessMult : HaftConstants.NonMetalStartingSharpnessMult;
        }

        //Checks if a given CollectableObject is made of metal
        public static bool IsCraftableMetal(this CollectibleObject collectibleObject, ICoreAPI api = null) {
            return collectibleObject.GetMetalItem(api) != null;
        }

        //Will return null if it is not a metal material!
        public static string GetMetalItem(this CollectibleObject collectibleObject, ICoreAPI api = null) {
            api ??= HaftModSystem.Api;
            var ingotItem = api?.World.GetItem(new AssetLocation("game:ingot-" + collectibleObject.GetMetalMaterial()));
            return ingotItem?.Variant["metal"] ?? ingotItem?.Variant["material"];
        }

        //Will return null if the given CollectibleObject does not have a 'metal' or 'material' variant typing!
        public static string GetMetalMaterial(this CollectibleObject collectibleObject) {
            return collectibleObject.Variant["metal"] ?? collectibleObject.Variant["material"];
        }

        //Since we know what the Head Durability Mult is, lets hook into GetMaxDurability for compat with other mods, let them do their things, THEN divide by the known HeadDurabilityMult to get the changed base value back.
        public static int GetBaseMaxDurability(this CollectibleObject collectibleObject, ItemStack itemStack) {
            return (int)((double)itemStack.Collectible.GetMaxDurability(itemStack) / HaftModSystem.Config.HeadDurabilityMult);
        }
    }
}
