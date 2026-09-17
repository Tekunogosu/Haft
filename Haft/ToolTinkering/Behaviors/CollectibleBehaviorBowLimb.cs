using System;
using System.Text;
using Haft.Client;
using Haft.Client.Behaviors;
using Haft.Config;
using Haft.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Haft.ToolTinkering.Behaviors {
    //Marks a collectible as having a bow limb - a stave, or a finished bow - and puts what that limb is made of, and
    //what it does, on the tooltip.
    //
    //The limb reads the SAME MaterialStats table a handle does. There are no bow-only stat numbers: draw power, draw
    //speed and limb life are all derived in HaftPartStatsHelpers from densityFactor, flexibility and speedBonus, so a
    //material that has a handle value has a bow value without anything further being written for it. Adding a wood or
    //a metal is one config entry, not one per system that consumes materials.
    public class CollectibleBehaviorBowLimb : CollectibleBehavior {

        //Every texture key the two vanilla stave shapes declare, across both variants. All of them point at plain
        //wood in vanilla, whatever they are named, so a stave recolours every one of them. Listed rather than read
        //off the shape because the only places that set them run server-side, with no tesselator to ask and an
        //unresolved variant placeholder in the item's own shape path.
        private static readonly string[] StaveWoodTextureKeys = { "aged", "bone", "leather", "pine" };

        public CollectibleBehaviorBowLimb(CollectibleObject collObj) : base(collObj) {

        }

        //Captures the wood at the moment the stave is sawn. Vanilla's recipe already restricts the log to a wood
        //variant and then throws that variant away, outputting a plain bowstave with no record of what it came from.
        //This reads it back off the input before it is lost, which is the whole hook the wood system hangs on.
        //
        //The wood is read from the block's "wood" variant rather than by position in its code. A handle can take
        //LastCodePart off its blank because supportbeam-oak and ingot-copper genuinely end in their material; a log
        //is log-placed-oak-ud and ends in its rotation, so the same call would record every stave as "ud". Asking for
        //the variant by name is also what keeps this working if a rotation or a type state is ever added.
        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, IRecipeBase byRecipe, ref EnumHandling bhHandling) {
            if (outputSlot as DummySlot != null) {
                return;
            }

            foreach (var slot in allInputslots) {
                if (slot.Empty || slot.Itemstack.Collectible.Tool != null) {
                    continue; //The saw is an input too, and carries a material of its own that is not the limb's.
                }

                //A stave sawn down from another stave carries its wood forward rather than re-reading it. That is the
                //long-to-recurve path, where the input already went through this once and the log is long gone.
                if (slot.Itemstack.HasLimbMaterialTag()) {
                    outputSlot.Itemstack.SetLimbMaterialTag(slot.Itemstack.GetLimbMaterialTag());
                    ApplyRenderingForLimb(outputSlot.Itemstack);
                    return;
                }

                var materialType = slot.Itemstack.Collectible.Variant["wood"];
                if (materialType != null && HaftModSystem.Stats.MaterialStats.ContainsKey(materialType)) {
                    outputSlot.Itemstack.SetLimbMaterialTag(materialType);
                    ApplyRenderingForLimb(outputSlot.Itemstack);
                    return;
                }
            }
        }

        //Builds the three-part render tree a composed bow renders from: limb, grip and string, each its own shape
        //with its own texture key, assembled into one mesh by ModularPartRenderingFromAttributes.
        //
        //The grip deliberately reuses the tool grip's stat table, texture key and stack attribute rather than
        //introducing bow-only versions. A wrap around a riser and a wrap around a haft are the same object, so a
        //grip added for tools becomes available to bows the moment it is defined, and a bow grip already carries
        //speedBonus and chanceToDamage that a later pass can read.
        //
        //The string has no stat of its own yet and takes the default below. That is the one axis of the three with
        //no plumbing behind it; adding it means a tag on the stack the way the limb has one.
        //A limb-bearing stack is drawn one of two ways and must not be given both trees. A stave recolours a single
        //shape, so it writes the single-part tree; a composed bow assembles from three part shapes, so it writes the
        //multi-part tree. GenMesh checks multi-part FIRST, so a stack carrying both would silently ignore whichever
        //was written for the other path - which is how a composed bow ended up rendering an untinted single shape.
        //
        //Pose 0 is the undrawn bow, which is where a freshly crafted one sits in an inventory.
        private static void ApplyRenderingForLimb(ItemStack stack) {
            if (stack == null) {
                return;
            }

            if (IsComposedFromParts(stack)) {
                ApplyBowPartRenderTree(stack, 0);
                return;
            }

            //A stave must not carry a multi-part tree. GetMultiPartRenderTree CREATES the tree as a side effect of
            //reading it, so a single call against a stave is enough to leave an EMPTY one behind - and GenMesh
            //checks multi-part first, finds it empty, and falls through to the untextured item default, ignoring the
            //single-part texture tree written just below. That is a stave rendering as a plain bow. Removing it here
            //also repairs staves crafted while that bug was live.
            stack.RemoveMultiPartRenderTree();
            ApplyLimbTexturesToRenderTree(stack);
        }

        //A bow composes only if it was built to: the parted item declares the rendering behavior AND ships the part
        //shapes. A stave carries the same behavior for its own recolouring, so the behavior alone is not the test.
        public static bool IsComposedFromParts(ItemStack stack) {
            return stack?.Collectible?.HasBehavior<ModularPartRenderingFromAttributes>() == true
                && stack.Collectible.Code?.Path?.StartsWith("bowparted") == true;
        }

        public static void ApplyBowPartRenderTree(ItemStack bow, int pose) {
            if (bow == null) {
                return;
            }

            //Nothing to do if this stack is already showing this pose. Worth checking because the work below is not
            //free and OnHeldInteractStep runs every tick, but mainly because the cache clear at the end forces a
            //full retesselation and doing that every tick would rebuild an identical mesh 30 times a second.
            if (bow.Attributes.GetInt(HaftAttributes.BowRenderedPose, -1) == pose) {
                return;
            }

            var tier = bow.Collectible.Variant["type"];
            if (tier == null) {
                return;
            }

            var multiPartTree = bow.GetMultiPartRenderTree();

            var limbTree = multiPartTree.GetPartAndTransformRenderTree(HaftAttributes.ModularPartLimbName).GetPartRenderTree();
            limbTree.SetPartShapePath(BowPartShapePath("limb", tier, pose));

            //Falls back to oak's texture rather than leaving the key unset: an unset key renders untextured, and a
            //bow with no recorded wood is a creative-spawned one rather than an error worth showing in-world.
            var limbStats = bow.GetLimbMaterialStats();
            var limbWood = limbStats != null ? bow.GetLimbMaterialTag() : HaftConstants.DefaultMaterialStatKey;
            limbTree.GetPartTextureTree().SetPartTexturePathFromKey("wood", HaftConstants.HandleWoodTexturePathMinusType + limbWood);

            var gripTree = multiPartTree.GetPartAndTransformRenderTree(HaftAttributes.ModularPartGripName).GetPartRenderTree();
            gripTree.SetPartShapePath(BowPartShapePath("grip", tier, pose));
            var gripStats = HaftModSystem.Stats.GripStats.Get(
                bow.HasHandleGripTag() ? bow.GetHandleGripTag() : HaftConstants.DefaultGripTag);
            if (gripStats != null && gripStats.texturePath != "plain") {
                gripTree.GetPartTextureTree().SetPartTexturePathFromKey("grip", gripStats.texturePath);
            }

            var stringTree = multiPartTree.GetPartAndTransformRenderTree(HaftAttributes.ModularPartStringName).GetPartRenderTree();
            stringTree.SetPartShapePath(BowPartShapePath("string", tier, pose));

            bow.Attributes.SetInt(HaftAttributes.BowRenderedPose, pose);

            //The composed mesh is built once and cached against HaftMeshID in TempAttributes, and every later render
            //reuses it. Repointing the render tree therefore changes nothing on its own - the bow keeps showing
            //whatever pose it was first tesselated at, which is a bow that raises but never draws. Clearing the id
            //is what makes the next render rebuild the mesh from the tree just written.
            bow.TempAttributes.RemoveAttribute(HaftAttributes.HaftMeshID);
        }

        private static string BowPartShapePath(string part, string tier, int pose) {
            return "haft:shapes/item/parts/bow/" + part + "/" + tier + "-draw" + pose;
        }

        //Points the stack's render tree at the wood it was made from, so a purpleheart stave looks like purpleheart.
        //
        //Reuses the handle's texture resolution exactly: a tool texture named for the species, falling back to the
        //debarked log texture for any wood that has no tool texture drawn for it. Both constants and both paths are
        //the handle's, so a wood added later gets a stave appearance the same moment it gets a handle one.
        //
        //Every texture key on both stave shapes points at wood already - long-stave's "bone" and "leather" keys are
        //debarked oak, not bone and not leather - so all of them are set rather than a chosen subset. That is only
        //true of the staves; the bow shapes use those same key names for real bone and real leather.
        private static void ApplyLimbTexturesToRenderTree(ItemStack stack) {
            if (!stack.HasLimbMaterialTag()) {
                return;
            }

            var materialType = stack.GetLimbMaterialTag();
            var materialTextPath = HaftConstants.HandleWoodTexturePathMinusType + materialType;
            if (!HaftModSystem.Api.Assets.Exists(new AssetLocation(materialTextPath + ".png"))) {
                materialTextPath = HaftConstants.DebarkedWoodBackupPathMinusType + materialType;
            }

            //Only the texture is recorded, not a shape path. The stave keeps the vanilla shape it already has, and
            //the renderer falls back to item.Shape for a tree with no shape path of its own - which is exactly what
            //is wanted until real per-part stave models exist.
            //
            //The shape's texture KEYS cannot be read here: this runs server-side during crafting and drying, where
            //there is no tesselator to ask, and the item's Shape.Base still holds its unresolved variant placeholder
            //("item/tool/bow/{type}-stave"), so it cannot be loaded by path either. The keys are therefore named
            //explicitly. Every one of them on both stave shapes points at plain wood - long-stave's "bone" and
            //"leather" are debarked oak, not bone and not leather - so colouring all of them is correct here, and is
            //NOT correct for the bow shapes, where those same names carry real bone and real leather.
            var textureTree = stack.GetPartRenderTree().GetPartTextureTree();
            foreach (var texCode in StaveWoodTextureKeys) {
                textureTree.SetPartTexturePathFromKey(texCode, materialTextPath);
            }
        }

        //Carries the limb material across drying. A raw stave becomes a dry one through transitionableProps, whose
        //transitionedStack is a plain item code - the engine clones that resolved stack, so the new stave is built
        //fresh and every attribute on the raw one, the wood included, is dropped. The wood is decided when the log is
        //sawn and cannot be recovered afterwards, so it has to survive the transition rather than be re-read.
        //
        //Done here rather than as a Harmony patch on the transition itself because the behavior is already on all
        //four stave variants: the base implementation hands each behavior the stack it is about to become, which is
        //exactly the hook needed, and nothing about the vanilla drying has to be replaced to use it.
        public override ItemStack OnTransitionNow(ItemSlot slot, TransitionableProperties props, ref EnumHandling handling) {
            //Nothing to carry: leave the result to the default path untouched, which is what PassThrough means. A
            //stave with no wood on it - creative-spawned, or from a save made before this existed - dries normally.
            if (slot?.Itemstack == null || !slot.Itemstack.HasLimbMaterialTag()) {
                return null;
            }

            var transitionedStack = props.TransitionedStack?.ResolvedItemstack?.Clone();
            if (transitionedStack == null) {
                return null;
            }

            transitionedStack.SetLimbMaterialTag(slot.Itemstack.GetLimbMaterialTag());

            //Re-resolved rather than copied across: the dry stave is a different item with its own shape, so its
            //texture keys and shape path are its own even though the wood is the same.
            ApplyRenderingForLimb(transitionedStack);

            //Once any behavior handles the transition the caller returns this stack directly and never reaches the
            //line that scales the stack size by the transition ratio, so that has to be applied here or a stack of
            //drying staves comes out as one.
            transitionedStack.StackSize = GameMath.RoundRandom(HaftModSystem.Api.World.Rand, slot.Itemstack.StackSize * props.TransitionRatio);

            handling = EnumHandling.PreventDefault;
            return transitionedStack;
        }

        //Scales how many shots the bow has in it by the limb's springback, the same way a tinkered tool scales its
        //durability by its parts. A limb that returns to shape is one that has not spent anything staying bent.
        //
        //This is the total-capacity half of limb life. The per-shot refund roll in BowStatHelper is the other half,
        //and they are kept apart on purpose: this decides how big the tank is, the refund decides how fast it drains,
        //and a treatment is meant to move the second without quietly changing the first.
        //
        //An uncrafted bow has no limb material and is left on the vanilla number rather than defaulted to oak's,
        //matching what the tooltip above says about it.
        public override int GetMaxDurability(ItemStack itemstack, int durability, ref EnumHandling bhHandling) {
            var materialStats = itemstack?.GetLimbMaterialStats();
            if (!HaftPartStatsHelpers.CanMaterialFormLimb(materialStats)) {
                return durability;
            }

            bhHandling = EnumHandling.PreventDefault;
            return (int)Math.Round(durability * HaftPartStatsHelpers.CalculateBowLimbDurability(materialStats));
        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo) {
            if (world == null || !world.Side.IsClient()) {
                return;
            }

            //Matches the handle tooltip's guard: ItemSlotCreative is a slot type while ShouldNotAccessStats tests the
            //inventory, so the two catch different things - the latter also covers the handbook's DummyInventory,
            //where a stack has no attribute data worth reading.
            if (inSlot is ItemSlotCreative || TinkeringUtility.ShouldNotAccessStats(inSlot)) {
                return;
            }

            //A material is recorded when the stave is sawn, so only a limb that was actually crafted has one. A
            //creative-spawned bow never passed through that step and genuinely has no limb material, so it says so
            //rather than printing the numbers for a wood nobody chose.
            if (!inSlot.Itemstack.HasLimbMaterialTag()) {
                dsc.AppendLine(Lang.Get("haftbowlimbunknown"));
                return;
            }

            var materialTag = inSlot.Itemstack.GetLimbMaterialTag();
            var materialStats = inSlot.Itemstack.GetLimbMaterialStats();

            dsc.AppendLine(Lang.Get("haftbowlimb", Lang.Get("material-" + materialTag)));

            //A material with no flexibility value cannot be a limb at all. That is the state every material a compat
            //mod adds starts in, so it is a normal thing to display rather than an error worth hiding.
            if (!HaftPartStatsHelpers.CanMaterialFormLimb(materialStats)) {
                return;
            }

            var drawWeight = HaftPartStatsHelpers.CalculateBowDrawWeight(materialStats);
            var drawSpeed = HaftPartStatsHelpers.CalculateBowDrawSpeed(materialStats);
            var limbDurability = HaftPartStatsHelpers.CalculateBowLimbDurability(materialStats);

            dsc.AppendLine(Lang.Get("haftbowdrawweight", StringHelpers.ColorForMultiplier(drawWeight), float.Truncate(drawWeight * 100) / 100));

            //Shown as the seconds a shot needs, not as a percent. Draw speed is now a multiplier on time rather
            //than a bonus, and the number a player can act on is how long they have to hold the bow - a percentage
            //would have to be read against a 0.65s gate that is never displayed anywhere.
            //
            //Colouring is inverted against the others on purpose: for every other stat a bigger number is better,
            //and here a bigger number is a slower bow.
            var drawSeconds = BowStatHelper.BaseDrawSeconds * drawSpeed;
            dsc.AppendLine(Lang.Get("haftbowdrawspeed", StringHelpers.ColorForMultiplier(1.0f / drawSpeed), float.Truncate(drawSeconds * 100) / 100));
            dsc.AppendLine(Lang.Get("haftbowlimbdurability", StringHelpers.ColorForMultiplier(limbDurability), float.Truncate(limbDurability * 100) / 100));

            //The treatment is read from the same attribute a haft's is. A bow is finished the same way a handle is,
            //so it carries the same tag rather than a second one meaning the same thing in saved world data; what
            //differs is which field of the treatment gets read, and limbRefundBonus is the bow's.
            var treatmentStats = HaftModSystem.Stats.TreatmentStats.Get(
                inSlot.Itemstack.HasHandleTreatmentTag() ? inSlot.Itemstack.GetHandleTreatmentTag() : HaftConstants.DefaultTreatmentTag);
            var refundChance = HaftPartStatsHelpers.CalculateBowLimbRefundChance(materialStats, treatmentStats);

            //Only shown when the limb actually has one. A bow at oak's baseline springback refunds nothing, and a
            //zero here would read as a broken stat rather than as the baseline it is.
            if (refundChance > 0.0f) {
                dsc.AppendLine(Lang.Get("haftbowlimbrefund", StringHelpers.ColorForBonus(refundChance), Math.Round(refundChance * 100)));
            }
        }
    }
}
