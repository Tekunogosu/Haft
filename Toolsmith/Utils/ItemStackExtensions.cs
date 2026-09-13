using System;
using System.Collections.Generic;
using Toolsmith.Client;
using Toolsmith.Compat;
using Toolsmith.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Toolsmith.Utils {
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
            if (!itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolHead)) {
                itemStack.ResetNullHead(ToolsmithModSystem.Api.World);
            }
            var head = itemStack.Attributes.GetItemstack(ToolsmithAttributes.ToolHead);
            var gotPart = false;
            if (head != null) {
                gotPart = head.ResolveBlockOrItem(ToolsmithModSystem.Api.World);
            }

            if (gotPart) {
                return head.Clone();
            } else {
                ToolsmithModSystem.Logger.Error("Unable to fully get and resolve Toolhead for ItemStack " + itemStack.Collectible.Code + " | Returning a Candle instead to prevent null errors. If a player recieves this, this is likely why!");
                return new ItemStack(ToolsmithModSystem.Api.World.GetItem(new AssetLocation(ToolsmithConstants.FallbackHeadCode)), 1);
            }
        }

        public static ItemStack? GetToolheadForData(this ItemStack itemStack) { //A version of the above call that specifically is to be used in cases where it is not mandatory for it to be set, and resetting it would be bad. It's either not a tool, or this is just for informational purposes.
            if (!itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolHead)) {
                return null;
            }
            var head = itemStack.Attributes.GetItemstack(ToolsmithAttributes.ToolHead);
            var gotPart = false;
            if (head != null) {
                gotPart = head.ResolveBlockOrItem(ToolsmithModSystem.Api.World);
            }

            if (gotPart) {
                return head.Clone();
            } else {
                ToolsmithModSystem.Logger.Error("Unable to fully get and resolve ToolheadForData for ItemStack " + itemStack.Collectible.Code + " | Returning Null instead, should be handled on the calling side, as it is possible to return null before this.");
                return null;
            }
        }

        public static void SetToolhead(this ItemStack itemStack, ItemStack toolhead) {
            itemStack.Attributes.SetItemstack(ToolsmithAttributes.ToolHead, toolhead.Clone()); //Stores a clone, so later changes to the caller's stack do not reach the tool.
        }

        public static int GetToolheadCurrentDurability(this ItemStack itemStack) { //Stored as the vanilla durability attribute, so other mods reading durability see the head's.
            itemStack.TestForOldToolheadAttributesAndFix();
            if (!itemStack.Attributes.HasAttribute(ToolsmithAttributes.Durability)) {
                itemStack.ResetNullHead(ToolsmithModSystem.Api.World);
            }
            return itemStack.Collectible.GetRemainingDurability(itemStack); //Trying to hook into the vanilla calls for compatability sake
        }

        public static void SetToolheadCurrentDurability(this ItemStack itemStack, int dur) {
            itemStack.Collectible.SetDurability(itemStack, dur);
        }

        public static bool HasToolheadCurrentDurability(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(ToolsmithAttributes.Durability);
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
            return head != null && head.Collectible.Code == ToolsmithConstants.FallbackHeadCode;
        }

        //Migrates a tool that stored its head durability under Toolsmith's own attributes onto the vanilla durability
        //attribute, then drops the old ones. Runs on read, so a tool from an older save migrates when first touched.
        public static void TestForOldToolheadAttributesAndFix(this ItemStack itemStack) {
            if (itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolHeadCurrentDur)) {
                itemStack.SetToolheadCurrentDurability(itemStack.Attributes.GetInt(ToolsmithAttributes.ToolHeadCurrentDur));
                itemStack.Attributes.RemoveAttribute(ToolsmithAttributes.ToolHeadCurrentDur);
            }
            if (itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolHeadMaxDur)) {
                itemStack.Attributes.RemoveAttribute(ToolsmithAttributes.ToolHeadMaxDur);
            }
        }

        //Sharpness!
        public static int GetToolCurrentSharpness(this ItemStack itemStack) {
            if (!itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolSharpnessCurrent)) {
                itemStack.ResetSharpness(ToolsmithModSystem.Api.World);
            }

            return itemStack.Attributes.GetInt(ToolsmithAttributes.ToolSharpnessCurrent);
        }

        public static void SetToolCurrentSharpness(this ItemStack itemStack, int sharp) {
            itemStack.Attributes.SetInt(ToolsmithAttributes.ToolSharpnessCurrent, sharp);
        }

        public static bool HasToolCurrentSharpness(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolSharpnessCurrent);
        }

        public static int GetToolMaxSharpness(this ItemStack itemStack) {
            if (!itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolSharpnessMax)) {
                itemStack.ResetSharpness(ToolsmithModSystem.Api.World);
            }
            return itemStack.Attributes.GetInt(ToolsmithAttributes.ToolSharpnessMax);
        }

        public static void SetToolMaxSharpness(this ItemStack itemStack, int sharp) {
            itemStack.Attributes.SetInt(ToolsmithAttributes.ToolSharpnessMax, sharp);
        }

        public static bool HasToolMaxSharpness(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolSharpnessMax);
        }

        public static float GetToolSharpnessPercent(this ItemStack itemStack) {
            var currentSharp = itemStack.GetToolCurrentSharpness();
            var maxSharp = itemStack.GetToolMaxSharpness();
            return ((float)currentSharp) / ((float)maxSharp);
        }

        public static void EnsureSharpnessIsNotOverMax(this ItemStack itemStack) {
            if (!itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolSharpnessCurrent) || !itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolSharpnessMax)) {
                itemStack.ResetSharpness(ToolsmithModSystem.Api.World);
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
            if (!itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolHandle)) {
                itemStack.ResetNullHandleOrBinding(ToolsmithModSystem.Api.World);
            }
            var handle = itemStack.Attributes.GetItemstack(ToolsmithAttributes.ToolHandle);
            var gotPart = false;
            if (handle != null) {
                gotPart = handle.ResolveBlockOrItem(ToolsmithModSystem.Api.World);
            }

            if (gotPart) {
                return handle.Clone();
            } else {
                ToolsmithModSystem.Logger.Error("Unable to fully get and resolve ToolHandle for ItemStack " + itemStack.Collectible.Code + " | Returning a Stick instead to prevent null errors. If a player recieves this, this is likely why!");
                return new ItemStack(ToolsmithModSystem.Api.World.GetItem(new AssetLocation(ToolsmithConstants.DefaultHandleCode)), 1);
            }
        }

        public static ItemStack? GetToolhandleForData(this ItemStack itemStack) { //A version of the above call that specifically is to be used in cases where it is not mandatory for it to be set, and resetting it would be bad. It's either not a tool, or this is just for informational purposes.
            if (!itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolHandle)) {
                return null;
            }
            var handle = itemStack.Attributes.GetItemstack(ToolsmithAttributes.ToolHandle);
            var gotPart = false;
            if (handle != null) {
                gotPart = handle.ResolveBlockOrItem(ToolsmithModSystem.Api.World);
            }

            if (gotPart) {
                return handle.Clone();
            } else {
                ToolsmithModSystem.Logger.Error("Unable to fully get and resolve ToolHandleForData for ItemStack " + itemStack.Collectible.Code + " | Returning Null instead, should be handled on the calling side, as it is possible to return null before this.");
                return null;
            }
        }

        public static void SetToolhandle(this ItemStack itemStack, ItemStack toolhandle) {
            itemStack.Attributes.SetItemstack(ToolsmithAttributes.ToolHandle, toolhandle.Clone());
        }

        public static int GetToolhandleCurrentDurability(this ItemStack itemStack) {
            return itemStack.Attributes.GetInt(ToolsmithAttributes.ToolHandleCurrentDur, itemStack.GetToolhandleMaxDurability());
        }

        public static void SetToolhandleCurrentDurability(this ItemStack itemStack, int dur) {
            itemStack.Attributes.SetInt(ToolsmithAttributes.ToolHandleCurrentDur, dur);
        }

        public static bool HasToolhandleCurrentDurability(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolHandleCurrentDur);
        }

        public static int GetToolhandleMaxDurability(this ItemStack itemStack) {
            if (!itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolHandleMaxDur)) {
                itemStack.ResetNullHandleOrBinding(ToolsmithModSystem.Api.World);
            }
            return itemStack.Attributes.GetInt(ToolsmithAttributes.ToolHandleMaxDur);
        }

        public static void SetToolhandleMaxDurability(this ItemStack itemStack, int dur) {
            itemStack.Attributes.SetInt(ToolsmithAttributes.ToolHandleMaxDur, dur);
        }

        public static bool HasToolhandleMaxDurability(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolHandleMaxDur);
        }

        public static void EnsureHandleIsNotOverMax(this ItemStack itemStack) {
            if (!itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolHandleCurrentDur) || !itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolHandleMaxDur)) {
                itemStack.ResetNullHandleOrBinding(ToolsmithModSystem.Api.World);
                return;
            }

            var maxDur = itemStack.GetToolhandleMaxDurability();
            if (itemStack.GetToolhandleCurrentDurability() > maxDur) {
                itemStack.SetToolhandleCurrentDurability(maxDur);
            }
        }

        public static ItemStack CheckForOldHandleAndConvert(ItemStack handle) {
            var oldHandlePath = handle.Collectible.Code.Path.Split('-');
            ToolsmithModSystem.Logger.Warning("OldHandle[0] is " + oldHandlePath[0]);
            if (oldHandlePath[0].Contains(ToolsmithAttributes.OldHandlePrefix)) {
                oldHandlePath[0] = oldHandlePath[0].Remove(0, 3);
            }
            ToolsmithModSystem.Logger.Warning("OldHandle[0] now is " + oldHandlePath[0]);
            if (handle.Collectible?.Code == null || !ToolsmithModSystem.Stats.BaseHandleParts.ContainsKey(handle.Collectible.Code.Path)) {
                ItemStack newHandle = new ItemStack(ToolsmithModSystem.Api.World.GetItem(new AssetLocation("toolsmith:" + oldHandlePath[0])));
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
                    newHandle.Attributes.RemoveAttribute(ToolsmithAttributes.ModularPartDataTree);
                }

                var multiPartTree = newHandle.GetMultiPartRenderTree();
                var handleTree = multiPartTree.GetPartAndTransformRenderTree(ToolsmithAttributes.ModularPartHandleName);
                var renderTree = handleTree.GetPartRenderTree();
                var textureTree = renderTree.GetPartTextureTree();

                if (oldHandlePath[0] == "handle" || oldHandlePath[0] == "carpentedhandle") {
                    HandlePartDefines handleStats = ToolsmithModSystem.Stats.BaseHandleParts.TryGetValue(oldHandlePath[0]);
                    newHandle.SetHandleStatTag(handleStats.handleStatTag);
                    newHandle.SetHandleMaterialTag(ToolsmithConstants.DefaultMaterialStatKey); //The old handles recorded no wood, so give the stat the same oak the texture below already assumes.
                    renderTree.SetPartShapePath(handleStats.handleShapePath);
                    textureTree.SetPartTexturePathFromKey("wood", ToolsmithConstants.HandleWoodTexturePathMinusType + "oak");
                }

                if (grip != null) {
                    var gripTree = multiPartTree.GetPartAndTransformRenderTree(ToolsmithAttributes.ModularPartGripName);
                    var gripRenderTree = gripTree.GetPartRenderTree();
                    var gripTextureTree = gripRenderTree.GetPartTextureTree();
                    var gripStats = ToolsmithModSystem.Stats.GripStats[grip];

                    newHandle.SetHandleGripTag(grip);
                    if (oldHandlePath[0] == "crude") {
                        gripRenderTree.SetPartShapePath("toolsmith:shapes/item/parts/handles/grips/gripfabric-crude");
                    } else {
                        gripRenderTree.SetPartShapePath("toolsmith:shapes/item/parts/handles/grips/gripfabric");
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
            if (!itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolBinding)) {
                return null;
            }
            var binding = itemStack.Attributes.GetItemstack(ToolsmithAttributes.ToolBinding);
            var gotPart = false;
            if (binding != null) {
                gotPart = binding.ResolveBlockOrItem(ToolsmithModSystem.Api.World);
            }

            if (gotPart) {
                return binding.Clone();
            } else {
                ToolsmithModSystem.Logger.Error("Unable to fully get and resolve ToolBinding for ItemStack " + itemStack.Collectible.Code + " | Returning Null instead, should be handled on the calling side, as it is possible to return null before this.");
                return null;
            }
        }

        public static void SetToolbinding(this ItemStack itemStack, ItemStack binding) {
            itemStack.Attributes.SetItemstack(ToolsmithAttributes.ToolBinding, binding.Clone());
        }

        public static int GetToolbindingCurrentDurability(this ItemStack itemStack) {
            return itemStack.Attributes.GetInt(ToolsmithAttributes.ToolBindingCurrentDur, itemStack.GetToolbindingMaxDurability());
        }

        public static void SetToolbindingCurrentDurability(this ItemStack itemStack, int dur) {
            itemStack.Attributes.SetInt(ToolsmithAttributes.ToolBindingCurrentDur, dur);
        }

        public static bool HasToolbindingCurrentDurability(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolBindingCurrentDur);
        }

        public static int GetToolbindingMaxDurability(this ItemStack itemStack) {
            if (!itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolBindingMaxDur)) {
                itemStack.ResetNullHandleOrBinding(ToolsmithModSystem.Api.World);
            }
            return itemStack.Attributes.GetInt(ToolsmithAttributes.ToolBindingMaxDur);
        }

        public static void SetToolbindingMaxDurability(this ItemStack itemStack, int dur) {
            itemStack.Attributes.SetInt(ToolsmithAttributes.ToolBindingMaxDur, dur);
        }

        public static bool HasToolbindingMaxDurability(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolBindingMaxDur);
        }

        public static void EnsureBindingIsNotOverMax(this ItemStack itemStack) {
            if (!itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolBindingCurrentDur) || !itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolBindingMaxDur)) {
                return;
            }

            var maxDur = itemStack.GetToolbindingMaxDurability();
            if (itemStack.GetToolbindingCurrentDurability() > maxDur) {
                itemStack.SetToolbindingCurrentDurability(maxDur);
            }
        }

        //Other Full Tool stats inhereted from the parts, set on crafting generally
        public static float GetGripChanceToDamage(this ItemStack itemStack) {
            return itemStack.Attributes.GetFloat(ToolsmithAttributes.GripChanceToDamage, 1.0f);
        }

        public static void SetGripChanceToDamage(this ItemStack itemStack, float chance) {
            itemStack.Attributes.SetFloat(ToolsmithAttributes.GripChanceToDamage, chance);
        }

        public static float GetSpeedBonus(this ItemStack itemStack) {
            return itemStack.Attributes.GetFloat(ToolsmithAttributes.SpeedBonus);
        }

        public static void SetSpeedBonus(this ItemStack itemStack, float speed) {
            itemStack.Attributes.SetFloat(ToolsmithAttributes.SpeedBonus, speed);
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
                ToolsmithModSystem.Logger.Error(reason);
                ToolsmithModSystem.IgnoreCodes.Add(itemStack.Collectible.Code.ToString());
                var headStackBackup = new ItemStack(world.GetItem(new AssetLocation(ToolsmithConstants.FallbackHeadCode)), 1); //Placeholder Candle! It'll be something so it actually _has_ something in there. No more nulls.
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
                var headDur = (int)(itemStack.Attributes.GetInt(ToolsmithAttributes.Durability, baseDur) * ToolsmithModSystem.Config.HeadDurabilityMult); //Keeps the damage the tool already carries on the head; the handle and binding get fresh stats.
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
                var headStack = new ItemStack(world.GetItem(new AssetLocation(ToolsmithConstants.FallbackHeadCode)), 1);
                var curHeadDur = (int)(itemStack.Attributes.GetInt(ToolsmithAttributes.Durability, baseDur) * ToolsmithModSystem.Config.HeadDurabilityMult); //Keeps whatever damage the tool already had on the head, while the handle and binding get fresh stats.
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
            int maxHandleDur = itemStack.Attributes.GetInt(ToolsmithAttributes.ToolHandleMaxDur, -1); //Specifically getting this from the attributes directly here to prevent looping the call.
            HandlePartDefines handleWithStats = null;

            ItemStack binding = null;
            int maxBindingDur = itemStack.Attributes.GetInt(ToolsmithAttributes.ToolBindingMaxDur, -1); //Specifically getting this from the attributes directly here to prevent looping the call.
            BindingPartDefines bindingWithStats = null;

            if (maxHandleDur < 0) { //Just need to grab the basic Stick if there is no durability already set.
                string handleCode = ToolsmithConstants.DefaultHandleCode;
                if (itemStack.Collectible.Code.Path.Contains("-bone")) {
                    handleCode = ToolsmithConstants.BoneHandleCode;
                }
                handle = new ItemStack(world.GetItem(new AssetLocation(handleCode)), 1);
                handleWithStats = ToolsmithModSystem.Stats.BaseHandleParts.TryGetValue(ToolsmithConstants.DefaultHandlePartKey);
            } else {
                handle = itemStack.GetToolhandle();
                handleWithStats = ToolsmithModSystem.Stats.BaseHandleParts.TryGetValue(handle.Collectible.Code.Path);
            }
            BindingStatDefines bindingStats;
            if (maxBindingDur < 0) { //Since 'None' is a valid binding, it still needs setting to that, and given the default durability levels! No Itemstack needed though.
                bindingStats = ToolsmithModSystem.Stats.BindingStats.Get(ToolsmithConstants.DefaultBindingPartKey);
            } else {
                binding = itemStack.GetToolbinding();
                bindingWithStats = ToolsmithModSystem.Stats.BindingParts.Get(binding.Collectible.Code.Path);
                bindingStats = ToolsmithModSystem.Stats.BindingStats.Get(bindingWithStats.bindingStatTag);
            }

            var handleStats = ToolsmithModSystem.Stats.BaseHandleStats.Get(handleWithStats.handleStatTag);
            var stats = ToolsmithPartStatsHelpers.ResolveHandleStats(handleStats, handle, bindingStats);

            var handleDur = ToolsmithPartStatsHelpers.CalculateHandleDurability(stats);
            var bindingDur = ToolsmithPartStatsHelpers.CalculateBindingDurability(stats);

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

            itemStack.SetSpeedBonus(ToolsmithPartStatsHelpers.CalculateSpeedBonus(stats));
            itemStack.SetGripChanceToDamage(ToolsmithPartStatsHelpers.CalculateGripChanceToDamage(stats));
        }

        // -- ItemStack Extensions for the Part items --
        public static void SetPartCurrentDurability(this ItemStack itemStack, int durability) {
            itemStack.Attributes.SetInt(ToolsmithAttributes.ToolPartCurrentDur, durability);
        }

        public static int GetPartCurrentDurability(this ItemStack itemStack) {
            return itemStack.Attributes.GetInt(ToolsmithAttributes.ToolPartCurrentDur, itemStack.GetPartMaxDurability());
        }

        public static bool HasPartCurrentDurability(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolPartCurrentDur);
        }

        public static void RemovePartCurrentDurability(this ItemStack itemStack) {
            itemStack.Attributes.RemoveAttribute(ToolsmithAttributes.ToolPartCurrentDur);
        }

        public static void SetPartMaxDurability(this ItemStack itemStack, int durability) {
            itemStack.Attributes.SetInt(ToolsmithAttributes.ToolPartMaxDur, durability);
        }

        public static int GetPartMaxDurability(this ItemStack itemStack) {
            return itemStack.Attributes.GetInt(ToolsmithAttributes.ToolPartMaxDur, 1000);
        }

        public static bool HasPartMaxDurability(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolPartMaxDur);
        }

        public static void RemovePartMaxDurability(this ItemStack itemStack) {
            itemStack.Attributes.RemoveAttribute(ToolsmithAttributes.ToolPartMaxDur);
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
            itemStack.Attributes.SetInt(ToolsmithAttributes.ToolSharpnessCurrent, sharpness);
        }

        public static int GetPartCurrentSharpness(this ItemStack itemStack) {
            if (!itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolSharpnessCurrent)) {
                if (!itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolPartCurrentDur)) {
                    itemStack.ResetHeadStats();
                } else {
                    itemStack.ResetHeadSharpness();
                }
            }

            return itemStack.Attributes.GetInt(ToolsmithAttributes.ToolSharpnessCurrent);
        }

        public static void SetPartMaxSharpness(this ItemStack itemStack, int sharpness) {
            itemStack.Attributes.SetInt(ToolsmithAttributes.ToolSharpnessMax, sharpness);
        }

        public static int GetPartMaxSharpness(this ItemStack itemStack) {
            if (!itemStack.Attributes.HasAttribute(ToolsmithAttributes.ToolSharpnessMax)) {
                itemStack.ResetHeadSharpness();
            }

            return itemStack.Attributes.GetInt(ToolsmithAttributes.ToolSharpnessMax);
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
            itemStack.Attributes.SetBool(ToolsmithAttributes.PartBeingCrafted, true);
        }

        public static void ClearPartBeingCrafted(this ItemStack itemStack) {
            itemStack.Attributes?.RemoveAttribute(ToolsmithAttributes.PartBeingCrafted);
        }

        public static bool PartBeingCrafted(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(ToolsmithAttributes.PartBeingCrafted);
        }

        public static void SetTotalHoneValue(this ItemStack itemStack, float honed) {
            itemStack.Attributes.SetFloat(ToolsmithAttributes.TotalHonedPercentSinceLastUse, honed);
        }

        public static float GetTotalHoneValue(this ItemStack itemStack) {
            return itemStack.Attributes.GetFloat(ToolsmithAttributes.TotalHonedPercentSinceLastUse); //If unset, it means it should get the 'first time honing' bonus of no durability loss.
        }

        public static bool HasTotalHoneValue(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(ToolsmithAttributes.TotalHonedPercentSinceLastUse);
        }

        public static void SetGrindstoneInUse(this ItemStack itemStack, float lastInterval) {
            if (itemStack.Attributes == null) {
                itemStack.Attributes = new TreeAttribute();
            }
            itemStack.Attributes.SetFloat(ToolsmithAttributes.GrindstoneInUse, lastInterval);
        }

        public static float GetGrindstoneInUse(this ItemStack itemStack) {
            return itemStack.Attributes.GetFloat(ToolsmithAttributes.GrindstoneInUse, 0.0f);
        }

        public static void ClearGrindstoneInUse(this ItemStack itemStack) {
            itemStack.Attributes?.RemoveAttribute(ToolsmithAttributes.GrindstoneInUse);
        }

        public static void SetWhetstoneInUse(this ItemStack itemStack, float lastInterval) {
            if (itemStack.Attributes == null) {
                itemStack.Attributes = new TreeAttribute();
            }
            itemStack.Attributes.SetFloat(ToolsmithAttributes.WhetstoneInUse, lastInterval);
        }

        public static float GetWhetstoneInUse(this ItemStack itemStack) {
            return itemStack.Attributes.GetFloat(ToolsmithAttributes.WhetstoneInUse);
        }

        public static void ClearWhetstoneInUse(this ItemStack itemStack) {
            itemStack.Attributes?.RemoveAttribute(ToolsmithAttributes.WhetstoneInUse);
        }

        public static bool WhetstoneInUse(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(ToolsmithAttributes.WhetstoneInUse);
        }

        public static void SetWhetstoneDoneSharpen(this ItemStack itemStack) {
            if (itemStack.Attributes == null) {
                itemStack.Attributes = new TreeAttribute();
            }
            itemStack.Attributes.SetBool(ToolsmithAttributes.WhetstoneDoneSharpen, true);
        }

        public static void ClearWhetstoneDoneSharpen(this ItemStack itemStack) {
            itemStack.Attributes?.RemoveAttribute(ToolsmithAttributes.WhetstoneDoneSharpen);
        }

        public static bool WhetstoneDoneSharpen(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(ToolsmithAttributes.WhetstoneDoneSharpen);
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
                    maxDur = (int)(baseDur * ToolsmithModSystem.Config.HeadDurabilityMult);

                    itemStack.SetPartMaxDurability(maxDur);
                    itemStack.SetPartCurrentDurability(maxDur);

                    maxSharp = ScientificSmithyCompat.CalculateMaxSharpness(itemStack, baseDur);
                    itemStack.SetPartMaxSharpness(maxSharp);
                    itemStack.SetPartCurrentSharpness((int)(itemStack.Collectible.StartingSharpnessMult() * maxSharp));
                    return;
                }
            }

            maxDur = itemStack.GetPartMaxDurability();
            itemStack.SetPartMaxDurability(maxDur);
            itemStack.SetPartCurrentDurability(maxDur);

            maxSharp = ScientificSmithyCompat.CalculateMaxSharpness(itemStack, (int)(maxDur / ToolsmithModSystem.Config.HeadDurabilityMult));
            itemStack.SetPartMaxSharpness(maxSharp);
            itemStack.SetPartCurrentSharpness((int)(itemStack.Collectible.StartingSharpnessMult() * maxSharp));
        }

        public static void ResetHeadSharpness(this ItemStack itemStack) {
            var maxDur = itemStack.GetPartMaxDurability();
            int maxSharp = ScientificSmithyCompat.CalculateMaxSharpness(itemStack, (int)(maxDur / ToolsmithModSystem.Config.HeadDurabilityMult));

            itemStack.SetPartMaxSharpness(maxSharp);
            itemStack.SetPartCurrentSharpness((int)(itemStack.Collectible.StartingSharpnessMult() * maxSharp));
        }

        public static void SetHandleStatTag(this ItemStack itemStack, string tag) {
            itemStack.Attributes.SetString(ToolsmithAttributes.HandleStatTag, tag);
        }

        public static string GetHandleStatTag(this ItemStack itemStack) {
            return itemStack.Attributes.GetString(ToolsmithAttributes.HandleStatTag);
        }

        public static bool HasHandleStatTag(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(ToolsmithAttributes.HandleStatTag);
        }

        public static void RemoveHandleStatTag(this ItemStack itemStack) {
            itemStack.Attributes.RemoveAttribute(ToolsmithAttributes.HandleStatTag);
        }

        //Everything a handle offers to a part being applied to it: the tags its own part define carries, plus the
        //tags of whatever it is made of. Gathering both is what lets a rule be written against the material without
        //splitting the handle parts per material - one "handle" part still covers all thirteen woods, and an oak
        //handle and a pine handle answer differently because their materials do.
        public static HashSet<string> GetHandleProvidedTags(this ItemStack handle) {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (handle?.Collectible?.Code != null) {
                var part = ToolsmithModSystem.Stats.BaseHandleParts.TryGetValue(handle.Collectible.Code.Path);
                if (part?.providesTags != null) {
                    tags.AddRange(part.providesTags);
                }
            }

            var materialStats = handle.GetHandleMaterialStats();
            if (materialStats?.providesTags != null) {
                tags.AddRange(materialStats.providesTags);
            }

            return tags;
        }

        //Resolves the material stats for a handle, in one place, because two callers need the same answer: the
        //material the stack records, else oak - the fallback a stick, a bone or a crude handle has always taken,
        //having no material of its own, and the one a handle that never passed through crafting takes as well.
        public static MaterialStatDefines GetHandleMaterialStats(this ItemStack handle) {
            if (handle.HasHandleMaterialTag()) {
                var tag = handle.GetHandleMaterialTag();
                if (tag != null && ToolsmithModSystem.Stats.MaterialStats.ContainsKey(tag)) {
                    return ToolsmithModSystem.Stats.MaterialStats.Get(tag);
                }
            }

            return ToolsmithModSystem.Stats.MaterialStats.Get(ToolsmithConstants.DefaultMaterialStatKey);
        }

        public static void SetHandleMaterialTag(this ItemStack itemStack, string tag) {
            itemStack.Attributes.SetString(ToolsmithAttributes.HandleMaterialTag, tag);
        }

        //Reads the material, migrating a handle saved under the old toolHandleWoodTag name as it goes. The migration
        //lives here rather than in a separate pass so that every caller gets it without having to remember to ask:
        //a handle from an older save is rewritten the first time anything looks at it, and a handle that has already
        //been migrated costs one HasAttribute check.
        public static string GetHandleMaterialTag(this ItemStack itemStack) {
            if (itemStack.Attributes.HasAttribute(ToolsmithAttributes.HandleMaterialTag)) {
                return itemStack.Attributes.GetString(ToolsmithAttributes.HandleMaterialTag);
            }

            if (itemStack.Attributes.HasAttribute(ToolsmithAttributes.LegacyHandleWoodTag)) {
                var legacy = itemStack.Attributes.GetString(ToolsmithAttributes.LegacyHandleWoodTag);
                itemStack.Attributes.SetString(ToolsmithAttributes.HandleMaterialTag, legacy);
                itemStack.Attributes.RemoveAttribute(ToolsmithAttributes.LegacyHandleWoodTag);
                return legacy;
            }

            return null;
        }

        public static bool HasHandleMaterialTag(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(ToolsmithAttributes.HandleMaterialTag) ||
                   itemStack.Attributes.HasAttribute(ToolsmithAttributes.LegacyHandleWoodTag);
        }

        public static void RemoveHandleMaterialTag(this ItemStack itemStack) {
            itemStack.Attributes.RemoveAttribute(ToolsmithAttributes.HandleMaterialTag);
            itemStack.Attributes.RemoveAttribute(ToolsmithAttributes.LegacyHandleWoodTag);
        }

        public static void SetHandleGripTag(this ItemStack itemStack, string tag) {
            itemStack.Attributes.SetString(ToolsmithAttributes.HandleGripTag, tag);
        }

        public static string GetHandleGripTag(this ItemStack itemStack) {
            return itemStack.Attributes.GetString(ToolsmithAttributes.HandleGripTag);
        }

        public static bool HasHandleGripTag(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(ToolsmithAttributes.HandleGripTag);
        }

        public static void SetHandleTreatmentTag(this ItemStack itemStack, string tag) {
            itemStack.Attributes.SetString(ToolsmithAttributes.HandleTreatmentTag, tag);
        }

        public static string GetHandleTreatmentTag(this ItemStack itemStack) {
            return itemStack.Attributes.GetString(ToolsmithAttributes.HandleTreatmentTag);
        }

        public static bool HasHandleTreatmentTag(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(ToolsmithAttributes.HandleTreatmentTag);
        }

        public static void SetGripAdhesiveTag(this ItemStack itemStack, string tag) {
            itemStack.Attributes.SetString(ToolsmithAttributes.GripAdhesiveTag, tag);
        }

        public static string GetGripAdhesiveTag(this ItemStack itemStack) {
            return itemStack.Attributes.GetString(ToolsmithAttributes.GripAdhesiveTag);
        }

        public static bool HasGripAdhesiveTag(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(ToolsmithAttributes.GripAdhesiveTag);
        }

        //What a grip offers a handle it is being attached to: its part define's tags, plus "adhesive-backed" when it
        //has actually been backed with one. The adhesive is a stack attribute rather than a part, so a rule about it
        //has to be answered from the stack - reading only the part define would miss it entirely.
        public static HashSet<string> GetGripProvidedTags(this ItemStack grip) {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (grip?.Collectible?.Code != null) {
                var part = ToolsmithModSystem.Stats.GripParts.TryGetValue(grip.Collectible.Code.Path);
                if (part?.providesTags != null) {
                    tags.AddRange(part.providesTags);
                }
            }

            if (grip.HasGripAdhesiveTag()) {
                tags.Add(ToolsmithConstants.AdhesiveBackedTag);
            }

            return tags;
        }

        public static void SetReadyToBlue(this ItemStack itemStack) {
            itemStack.Attributes.SetBool(ToolsmithAttributes.PartReadyToBlue, true);
        }

        public static bool IsReadyToBlue(this ItemStack itemStack) {
            return itemStack.Attributes.GetBool(ToolsmithAttributes.PartReadyToBlue, false);
        }

        public static void SetWetTreatment(this ItemStack itemStack, int hours) {
            itemStack.Attributes.SetInt(ToolsmithAttributes.PartWetTreatment, hours);
        }

        public static int GetWetTreatment(this ItemStack itemStack) {
            return itemStack.Attributes.GetInt(ToolsmithAttributes.PartWetTreatment);
        }

        public static bool HasWetTreatment(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(ToolsmithAttributes.PartWetTreatment);
        }

        public static void RemoveWetTreatment(this ItemStack itemStack) {
            itemStack.Attributes.RemoveAttribute(ToolsmithAttributes.PartWetTreatment);
            itemStack.Attributes.RemoveAttribute(ToolsmithAttributes.TransitionState);

            var multiPartTree = itemStack.GetMultiPartRenderTree();
            var partAndTransformTree = multiPartTree.GetPartAndTransformRenderTree(ToolsmithAttributes.ModularPartHandleName);
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
            itemStack.Attributes.SetBool(ToolsmithAttributes.DisposeMeNowPleaseTag, true);
        }

        public static bool HasDisposeMeNowPlease(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(ToolsmithAttributes.DisposeMeNowPleaseTag);
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
                ToolsmithModSystem.Logger.Error("Something went wrong attempting to add a behavior to the provided Collectable with code: " + collectibleObject.Code + "\nIf this isn't an intended Tool or Part, try adding it to the blacklist to avoid this in the future!");
                ToolsmithModSystem.Logger.Error(ex);
            }
        }

        //How sharp a freshly made edge starts out, as a share of its maximum. Metal takes and holds a keener edge
        //than bone, flint or obsidian, so the two are banded rather than sharing one figure.
        public static float StartingSharpnessMult(this CollectibleObject collectibleObject) {
            return collectibleObject.IsCraftableMetal() ? ToolsmithConstants.StartingSharpnessMult : ToolsmithConstants.NonMetalStartingSharpnessMult;
        }

        //Checks if a given CollectableObject is made of metal
        public static bool IsCraftableMetal(this CollectibleObject collectibleObject, ICoreAPI api = null) {
            return collectibleObject.GetMetalItem(api) != null;
        }

        //Will return null if it is not a metal material!
        public static string GetMetalItem(this CollectibleObject collectibleObject, ICoreAPI api = null) {
            api ??= ToolsmithModSystem.Api;
            var ingotItem = api?.World.GetItem(new AssetLocation("game:ingot-" + collectibleObject.GetMetalMaterial()));
            return ingotItem?.Variant["metal"] ?? ingotItem?.Variant["material"];
        }

        //Will return null if the given CollectibleObject does not have a 'metal' or 'material' variant typing!
        public static string GetMetalMaterial(this CollectibleObject collectibleObject) {
            return collectibleObject.Variant["metal"] ?? collectibleObject.Variant["material"];
        }

        //Since we know what the Head Durability Mult is, lets hook into GetMaxDurability for compat with other mods, let them do their things, THEN divide by the known HeadDurabilityMult to get the changed base value back.
        public static int GetBaseMaxDurability(this CollectibleObject collectibleObject, ItemStack itemStack) {
            return (int)((double)itemStack.Collectible.GetMaxDurability(itemStack) / ToolsmithModSystem.Config.HeadDurabilityMult);
        }
    }
}
