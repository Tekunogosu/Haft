using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Toolsmith.Client;
using Toolsmith.Client.Behaviors;
using Toolsmith.Config;
using Toolsmith.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Common;
using Vintagestory.GameContent;

namespace Toolsmith.ToolTinkering.Behaviors {
    public class CollectibleBehaviorToolHandle : CollectibleBehaviorToolPartWithHealth, IModularPartRenderer { //Mostly here just to allow for easy detection if something is a tool handle!

        public CollectibleBehaviorToolHandle(CollectibleObject collObj) : base(collObj) {

        }

        public override void OnLoaded(ICoreAPI api) {
            base.OnLoaded(api);
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo) {
            //ItemSlotCreative is a slot type and ShouldNotAccessStats tests the inventory, so the two catch
            //different things - the latter also covers the handbook's DummyInventory and a trader's inventory,
            //where a handle has no attribute data worth reading.
            if (inSlot is ItemSlotCreative || TinkeringUtility.ShouldNotAccessStats(inSlot)) {
                dsc.AppendLine(Lang.Get("toolhandledirections"));
                base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
                return;
            }

            dsc.AppendLine(Lang.Get("toolhandledirections"));
            if (inSlot.Itemstack.HasHandleGripTag() && inSlot.Itemstack.HasHandleTreatmentTag()) {
                dsc.AppendLine(Lang.Get("toolhandlefullyprepared", inSlot.Itemstack.GetHandleTreatmentTag(), inSlot.Itemstack.GetHandleGripTag()));
            } else if (inSlot.Itemstack.HasHandleTreatmentTag()) {
                dsc.AppendLine(Lang.Get("toolhandletreated", inSlot.Itemstack.GetHandleTreatmentTag()));
            } else if (inSlot.Itemstack.HasHandleGripTag()) {
                dsc.AppendLine(Lang.Get("toolhandlegripped", inSlot.Itemstack.GetHandleGripTag()));
            }
            base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);

            if (world.Api.Side.IsClient()) {
                var handleStats = ToolsmithModSystem.Stats.BaseHandleStats.Get(ToolsmithModSystem.Stats.BaseHandleParts.Get(inSlot.Itemstack.Collectible.Code.Path).handleStatTag);
                if (handleStats != null) {
                    var totalHandleMult = handleStats.baseHPfactor * (1 + handleStats.selfHPBonus);
                    dsc.AppendLine("");

                    //A material is recorded when the handle is made, so only a handle that was actually crafted has one.
                    //A creative-spawned handle never passed through crafting and genuinely has no material, the same
                    //as a stick or a bone, so say so rather than printing the oak the stats fall back to as though it
                    //had been chosen.
                    if (inSlot.Itemstack.HasHandleMaterialTag()) {
                        var materialTag = inSlot.Itemstack.GetHandleMaterialTag();
                        var materialStats = ToolsmithModSystem.Stats.MaterialStats.Get(materialTag);
                        dsc.AppendLine(Lang.Get("toolhandlewood", Lang.Get("material-" + materialTag)));
                        if (materialStats != null) {
                            dsc.AppendLine(Lang.Get("toolhandlewooddensity", StringHelpers.ColorForMultiplier(materialStats.densityFactor), materialStats.densityFactor));
                        }
                    } else if (ToolsmithModSystem.Stats.BaseHandleParts.Get(inSlot.Itemstack.Collectible.Code.Path)?.canBeTreated == true) {
                        dsc.AppendLine(Lang.Get("toolhandlewoodunknown")); //Only worth saying on a handle that could have had a material in the first place.
                    }

                    dsc.AppendLine(Lang.Get("toolhandletotalmult", StringHelpers.ColorForMultiplier(totalHandleMult), float.Truncate(totalHandleMult * 100) / 100));
                    dsc.AppendLine(Lang.Get("toolhandlebindingbonus", StringHelpers.ColorForBonus(handleStats.bindingHPBonus), Math.Round(handleStats.bindingHPBonus * 100)));
                    dsc.AppendLine(Lang.Get("toolhandleusespeedbonus", StringHelpers.ColorForBonus(handleStats.speedBonus), Math.Round(handleStats.speedBonus * 100)));
                    if (inSlot.Itemstack.HasHandleTreatmentTag()) {
                        var treatmentStats = ToolsmithModSystem.Stats.TreatmentStats.Get(inSlot.Itemstack.GetHandleTreatmentTag());
                        if (treatmentStats != null) {
                            dsc.AppendLine(Lang.Get("toolhandletreatmentbonus", StringHelpers.ColorForBonus(treatmentStats.handleHPbonus), Math.Round(treatmentStats.handleHPbonus * 100)));
                            //A treatment that also sheds wear says so on its own line, next to the grip's equivalent.
                            //Silent when it saves nothing, so an untreated-for-wear treatment prints no empty claim.
                            if (treatmentStats.chanceToDamageReduction > 0.0f) {
                                dsc.AppendLine(Lang.Get("toolhandletreatmentwear", StringHelpers.ColorForBonus(treatmentStats.chanceToDamageReduction), Math.Round(treatmentStats.chanceToDamageReduction * 100)));
                            }
                        }
                    }
                    if (inSlot.Itemstack.HasHandleGripTag()) {
                        var gripStats = ToolsmithModSystem.Stats.GripStats.Get(inSlot.Itemstack.GetHandleGripTag());
                        if (gripStats != null) {
                            dsc.AppendLine(Lang.Get("toolhandlegripspeedbonus", StringHelpers.ColorForBonus(gripStats.speedBonus), Math.Round(gripStats.speedBonus * 100)));
                            dsc.AppendLine(Lang.Get("toolhandlegripchancenodamage", StringHelpers.ColorForBonus(1 - gripStats.chanceToDamage), Math.Round((1 - gripStats.chanceToDamage) * 100)));
                        }
                    }
                }
            }
        }

        public override void GetHeldItemName(StringBuilder sb, ItemStack itemStack) {
            //Named the way the base game names its own wood-typed blocks - "Support beam (Oak)" - so a row of
            //handles in an inventory can be told apart without opening each tooltip.
            if (itemStack.HasHandleMaterialTag()) {
                sb.Append(" (" + Lang.Get("material-" + itemStack.GetHandleMaterialTag()) + ")");
            }
            if (itemStack.HasWetTreatment()) {
                sb.Append(Lang.Get("handleiswet"));
            }
        }

        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, IRecipeBase byRecipe, ref EnumHandling bhHandling) {
            if (outputSlot as DummySlot != null) {
                return;
            }

            ItemSlot toolSlot = null;
            ItemSlot blankSlot = null;
            ItemSlot handleSlot = null;
            ItemSlot gripOrTreatmentSlot = null;

            foreach (var slot in allInputslots) {
                if (!slot.Empty && (slot.Itemstack.Collectible.Code != ToolsmithConstants.SandpaperCode || slot.Itemstack.Collectible.Code != ToolsmithConstants.FirewoodCode)) {
                    if (slot.Itemstack.Collectible.Tool != null) {
                        toolSlot = slot;
                    } else if (slot.Itemstack.Collectible.Code.FirstCodePart() == ToolsmithConstants.HandleBlankCode ||
                               slot.Itemstack.Collectible.Code.FirstCodePart() == ToolsmithConstants.MetalHandleBlankCode ||
                               slot.Itemstack.Collectible.Code.FirstCodePart() == ToolsmithConstants.WorkItemFirstCodePart) {
                        blankSlot = slot;
                    } else if (TinkeringUtility.IsValidHandle(slot.Itemstack)) {
                        handleSlot = slot;
                    } else if (slot.Itemstack != null) {
                        /*if (slot.Itemstack.Collectible.Code.Path.StartsWith(ToolsmithAttributes.OldHandlePrefix)) { //If we find an old handle it's time to convert it to the new ones. Remove this bit later on after some time.
                            outputSlot.Itemstack = ItemStackExtensions.CheckForOldHandleAndConvert(slot.Itemstack);
                            return;
                        }*/
                        gripOrTreatmentSlot = slot;
                    }
                }
            }

            //Smithing hands in only the work item - there is no hammer among the input slots the way a grid recipe has
            //its knife - so the blank alone is the whole input there. A grid recipe still requires its tool.
            var isSmithedBlank = blankSlot != null && blankSlot.Itemstack.Collectible.Code.FirstCodePart() == ToolsmithConstants.WorkItemFirstCodePart;
            if ((toolSlot != null || isSmithedBlank) && blankSlot != null) {
                //Both an ingot (grid) and a work item (anvil) are metal blanks. Missing the work item here would send a
                //smithed handle down the wood texture path and look for a tool texture named after a metal.
                var isMetalBlank = blankSlot.Itemstack.Collectible.Code.FirstCodePart() == ToolsmithConstants.MetalHandleBlankCode || isSmithedBlank;
                var materialType = blankSlot.Itemstack.Collectible.LastCodePart();
                if (!TinkeringUtility.IsStickOrBone(outputSlot.Itemstack) && materialType != null) {
                    if (!isMetalBlank && (materialType == "veryaged" || materialType == "veryagedrotten")) {
                        materialType = "aged";
                    }
                    ITreeAttribute multiPartTree = outputSlot.Itemstack.GetMultiPartRenderTree();
                    ITreeAttribute handlePartAndTransformTree = multiPartTree.GetPartAndTransformRenderTree(ToolsmithAttributes.ModularPartHandleName);
                    ITreeAttribute handleRenderTree = handlePartAndTransformTree.GetPartRenderTree();
                    ITreeAttribute handleTextureTree = handleRenderTree.GetPartTextureTree();

                    //Wood and metal differ only in where the texture comes from. A wood has a tool-specific texture
                    //with a debarked fallback; a metal uses the plate texture the bindings already use, so a metal
                    //that exists as an ingot has one without anything further being drawn.
                    string materialTextPath;
                    if (isMetalBlank) {
                        materialTextPath = ToolsmithConstants.HandleMetalTexturePathMinusType + materialType;
                        if (!ToolsmithModSystem.Api.Assets.Exists(new AssetLocation(materialTextPath + ".png"))) {
                            materialTextPath = ToolsmithConstants.IngotMetalBackupPathMinusType + materialType;
                        }
                    } else {
                        materialTextPath = ToolsmithConstants.HandleWoodTexturePathMinusType + materialType;
                        if (!ToolsmithModSystem.Api.Assets.Exists(new AssetLocation(materialTextPath + ".png"))) {
                            materialTextPath = ToolsmithConstants.DebarkedWoodBackupPathMinusType + materialType;
                        }
                    }
                    handleTextureTree.SetPartTexturePathFromKey("wood", materialTextPath);
                    outputSlot.Itemstack.SetHandleMaterialTag(materialType); //Already collapsed to a key the material stats hold - the veryaged/veryagedrotten folding above runs first.
                    HandlePartDefines handleStats = ToolsmithModSystem.Stats.BaseHandleParts.TryGetValue(outputSlot.Itemstack.Collectible.Code.Path);
                    handleRenderTree.SetPartShapePath(handleStats.handleShapePath);
                    outputSlot.Itemstack.SetHandleStatTag(handleStats.handleStatTag);
                    outputSlot.Itemstack.SetPartCurrentDurability(1000);
                    outputSlot.Itemstack.SetPartMaxDurability(1000);
                    bhHandling = EnumHandling.Handled;
                }
            } else if (handleSlot != null && gripOrTreatmentSlot != null) {
                outputSlot.Itemstack.Attributes = handleSlot.Itemstack.Attributes.Clone();
                ITreeAttribute multiPartTree = outputSlot.Itemstack.GetMultiPartRenderTree();
                ITreeAttribute handlePartAndTransformTree = multiPartTree.GetPartAndTransformRenderTree(ToolsmithAttributes.ModularPartHandleName);

                if (handleSlot.Itemstack.HasPartRenderTree()) { //If this is a spawned-in handle from creative, this will catch it and convert from a PartRenderTree to a MultiPartRenderTree.
                    ITreeAttribute handlePartTree = handleSlot.Itemstack.GetPartRenderTree().Clone();
                    outputSlot.Itemstack.RemovePartRenderTree();
                    handlePartAndTransformTree.SetPartRenderTree(handlePartTree);
                } else { //This should catch the case of a Bone or other handle type and build a possible shape string for them based on it's name.
                    HandlePartDefines handleStats = ToolsmithModSystem.Stats.BaseHandleParts.TryGetValue(outputSlot.Itemstack.Collectible.Code.Path);
                    ITreeAttribute handlePartTree = handlePartAndTransformTree.GetPartRenderTree();
                    handlePartTree.SetPartShapePath(handleStats.handleShapePath);
                }
                
                if (ToolsmithModSystem.Stats.GripParts.ContainsKey(gripOrTreatmentSlot.Itemstack.Collectible.Code.Path)) {
                    //Same two refusals, in order: a handle already wearing a grip, then a grip this handle cannot
                    //take. The second is what lets a smooth metal handle demand an adhesive-backed grip while a
                    //wooden one accepts any - see the tag comment on ToolsmithPart.
                    if (handleSlot.Itemstack.HasHandleGripTag() ||
                            !ConfigUtility.TagsSatisfy(ToolsmithModSystem.Stats.GripParts[gripOrTreatmentSlot.Itemstack.Collectible.Code.Path]?.requiresTags,
                                                       handleSlot.Itemstack.GetHandleProvidedTags())) {
                        outputSlot.Itemstack = null;
                        outputSlot.Itemstack = new ItemStack(ToolsmithModSystem.Api.World.GetBlock(new AssetLocation("game:air")));
                        outputSlot.Itemstack.SetDisposeMeNowPlease();
                        bhHandling = EnumHandling.PreventDefault;
                    } else {
                        var grip = gripOrTreatmentSlot.Itemstack;
                        var gripWithStats = ToolsmithModSystem.Stats.GripParts[grip.Collectible.Code.Path];
                        var gripStats = ToolsmithModSystem.Stats.GripStats[gripWithStats.gripStatTag];
                        ITreeAttribute gripPartTree = multiPartTree.GetPartAndTransformRenderTree(ToolsmithAttributes.ModularPartGripName);
                        ITreeAttribute gripRenderTree = gripPartTree.GetPartRenderTree();
                        ITreeAttribute gripTextureTree = gripRenderTree.GetPartTextureTree();
                        /*if (handleSlot.Itemstack.Collectible.Code.Path == "crudehandle") {
                            gripRenderTree.SetShapeOverrideTag("-crude");
                        }*/

                        HandlePartDefines handleStats = ToolsmithModSystem.Stats.BaseHandleParts.TryGetValue(outputSlot.Itemstack.Collectible.Code.Path);
                        //var splitHandlePath = handleStats.handleShapePath.Split('/');
                        var gripPath = MultiPartRenderingHelpers.ConvertFromGenericHandlePathToGripShapePath(handleStats.handleShapePath, gripWithStats.gripShapePath);
                            /*splitHandlePath[0];
                        for (int i = 1; i < splitHandlePath.Length - 1; i++) {
                            gripPath = gripPath + "/" + splitHandlePath[i];
                        }
                        gripPath = gripPath + "/grip/" + gripWithStats.gripShapePath;*/

                        gripRenderTree.SetPartShapePath(gripPath);
                        outputSlot.Itemstack.SetHandleGripTag(gripStats.id);
                        if (gripWithStats.gripTextureOverride != "") {
                            gripTextureTree.SetPartTexturePathFromKey("grip", gripWithStats.gripTextureOverride);
                        } else {
                            gripTextureTree.SetPartTexturePathFromKey("grip", gripStats.texturePath);
                        }
                        bhHandling = EnumHandling.Handled;
                    }
                } else {
                    if (handleSlot.Itemstack.HasHandleGripTag() || handleSlot.Itemstack.HasHandleTreatmentTag()) {
                        outputSlot.Itemstack = null;
                        outputSlot.Itemstack = new ItemStack(ToolsmithModSystem.Api.World.GetBlock(new AssetLocation("game:air")));
                        outputSlot.Itemstack.SetDisposeMeNowPlease();
                        bhHandling = EnumHandling.PreventDefault;
                    } else {
                        ITreeAttribute handlePartTree = multiPartTree.GetPartAndTransformRenderTree(ToolsmithAttributes.ModularPartHandleName);
                        ITreeAttribute handleRenderTree = handlePartTree.GetPartRenderTree();
                        ITreeAttribute handleTextureTree = handleRenderTree.GetPartTextureTree();
                        var treatment = gripOrTreatmentSlot.Itemstack;

                        if (treatment.Class == EnumItemClass.Block && (treatment.Block as ILiquidInterface) != null) {
                            treatment = (treatment.Block as ILiquidInterface).GetContent(treatment);
                        }

                        var treatmentStatPair = ToolsmithModSystem.Stats.TreatmentParts.TryGetValue(treatment.Collectible.Code.Path);

                        //A treatment can demand things of what it is applied to - beeswax asking for wood, a bluing
                        //salt asking for metal. The recipe cannot filter this, because treatment recipes are built per
                        //handle PART and one part covers every wood, so the check belongs here where the handle's
                        //actual material is known. Refused the same way an already-treated handle is.
                        if (!ConfigUtility.TagsSatisfy(treatmentStatPair?.requiresTags, handleSlot.Itemstack.GetHandleProvidedTags())) {
                            outputSlot.Itemstack = null;
                            outputSlot.Itemstack = new ItemStack(ToolsmithModSystem.Api.World.GetBlock(new AssetLocation("game:air")));
                            outputSlot.Itemstack.SetDisposeMeNowPlease();
                            bhHandling = EnumHandling.PreventDefault;
                            return;
                        }

                        var treatmentStats = ToolsmithModSystem.Stats.TreatmentStats.TryGetValue(treatmentStatPair.treatmentStatTag);
                        var handleStatPair = ToolsmithModSystem.Stats.BaseHandleParts.TryGetValue(handleSlot.Itemstack.Collectible.Code.Path);
                        outputSlot.Itemstack.SetHandleTreatmentTag(treatmentStats.id);
                        outputSlot.Itemstack.SetWetTreatment((int)(treatmentStatPair.dryingHours * handleStatPair.dryingTimeMult));
                        outputSlot.Itemstack.Collectible.SetTransitionState(outputSlot.Itemstack, EnumTransitionType.Dry, 0);

                        if (treatmentStatPair.isLiquid) {
                            handleTextureTree.SetPartTexturePathFromKey("wood-overlay", ToolsmithConstants.DarkTreatementOverlayPath);
                        } else if (!treatmentStatPair.isLiquid) {
                            handleTextureTree.SetPartTexturePathFromKey("wood-overlay", ToolsmithConstants.LightTreatementOverlayPath);
                        } else {
                            ToolsmithModSystem.Logger.Error("A treatment config is improperly set! This treatment - " + treatment.Collectible.Code + " - was marked as a liquid, but could not find a liquid container in this recipe. Will treat it as a non-liquid, but is worth fixing that!");
                            handleTextureTree.SetPartTexturePathFromKey("wood-overlay", ToolsmithConstants.LightTreatementOverlayPath);
                        }
                        bhHandling = EnumHandling.Handled;
                    }
                }
            } else if (handleSlot != null) {
                if (!TinkeringUtility.IsStickOrBone(outputSlot.Itemstack)) {
                    outputSlot.Itemstack.Attributes = handleSlot.Itemstack.Attributes.Clone();
                }
            } else { //If it hits this, it's likely someone crafted a more basic handle somehow, like a crude handle. Lets initialize it so it can at least have the trees.
                if (!TinkeringUtility.IsStickOrBone(outputSlot.Itemstack)) {
                    var renderTree = outputSlot.Itemstack.GetPartRenderTree();
                    renderTree.GetPartTextureTree();
                }
                
                if (handleSlot == null && gripOrTreatmentSlot != null && ToolsmithModSystem.Stats.GripParts.ContainsKey(gripOrTreatmentSlot.Itemstack.Collectible.Code.Path)) { //Copying this down here as well since the changes to the 'valid handle' check resulted in not returning a valid handle when it has a Wet Treatment, so it never hit this check when trying to apply a grip.
                    outputSlot.Itemstack = null;
                    outputSlot.Itemstack = new ItemStack(ToolsmithModSystem.Api.World.GetBlock(new AssetLocation("game:air")));
                    outputSlot.Itemstack.SetDisposeMeNowPlease();
                    bhHandling = EnumHandling.PreventDefault;
                }
            }

            outputSlot.MarkDirty();
        }

        public ITreeAttribute InitializeRenderTree(ITreeAttribute tree, Item item) { //This is sent the MultiPartRenderTree, but only ever used for Creative spawned items.
            var top = tree.GetOrAddTreeAttribute(ToolsmithAttributes.ModularPartDataTree);
            top.GetPartTextureTree();
            var handleStatsPair = ToolsmithModSystem.Stats.BaseHandleParts.TryGetValue(item.Code.Path);
            top.SetPartShapePath(handleStatsPair.handleShapePath);
            return tree;
        }

        public void ResetRotationAndOffset(ItemStack handle) {
            if (TinkeringUtility.CheckForAndScrubStickBone(handle)) {
                return;
            }
            var tree = handle.GetMultiPartRenderTree();

            if (tree.HasAttribute(ToolsmithAttributes.ModularPartHandleName)) {
                var handlePartAndTransform = tree.GetPartAndTransformRenderTree(ToolsmithAttributes.ModularPartHandleName);
                handlePartAndTransform.SetPartRotationX(0);
                handlePartAndTransform.SetPartRotationY(0);
                handlePartAndTransform.SetPartRotationZ(0);
                handlePartAndTransform.SetPartOffsetX(0);
                handlePartAndTransform.SetPartOffsetY(0);
                handlePartAndTransform.SetPartOffsetZ(0);
            }

            if (tree.HasAttribute(ToolsmithAttributes.ModularPartGripName)) {
                var gripPartAndTransform = tree.GetPartAndTransformRenderTree(ToolsmithAttributes.ModularPartGripName);
                gripPartAndTransform.SetPartRotationX(0);
                gripPartAndTransform.SetPartRotationY(0);
                gripPartAndTransform.SetPartRotationZ(0);
                gripPartAndTransform.SetPartOffsetX(0);
                gripPartAndTransform.SetPartOffsetY(0);
                gripPartAndTransform.SetPartOffsetZ(0);
            }
        }

        public override bool RequiresTransitionableTicking(IWorldAccessor world, ItemStack itemstack, ref EnumHandling handling) {
            handling = EnumHandling.PreventDefault;

            if (itemstack.HasWetTreatment()) {
                return true;
            }

            return false;
        }
    }
}
