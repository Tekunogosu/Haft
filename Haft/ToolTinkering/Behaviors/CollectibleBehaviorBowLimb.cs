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
    //speed and limb life are all derived in HaftPartStatsHelpers from density, flexibility and speedBonus, so a
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

            //Wrapping the riser of a bow that already exists, rather than building a new one. It is tested FIRST
            //because the bow handed in carries a limb material of its own, which would otherwise be read by the
            //carry-forward branch below as though a stave had been sawn down - copying the wood across correctly
            //and dropping the grip on the floor.
            if (TryApplyGripCraft(allInputslots, outputSlot)) {
                return;
            }

            //A tier with no wood axis records nothing, however good its inputs were. A crude bow is lashed from
            //sticks, and a stick that happens to name a species does not make the bow a bow of that species - the
            //same reason a crude handle keeps no record of the firewood it came from. Its rendering still runs, so
            //it takes the default wood's texture rather than none at all.
            if (!outputSlot.Itemstack.BowHasWoodAxis()) {
                ApplyRenderingForLimb(outputSlot.Itemstack);
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

        //Refuses a grip craft whose bow is already gripped, at the point the game decides whether the recipe matches
        //at all. Returning false here means the craft never appears: no output is previewed and nothing is consumed.
        //
        //The check has to live here rather than in OnCreatedByCrafting because by the time that runs the recipe has
        //already matched and the grid has already been read. Refusing there produces a craft that looked valid,
        //swallowed the grip and returned an unchanged bow - which is exactly what wrapping a second grip did.
        //
        //Every other recipe is passed through untouched; this only ever answers for a two-slot bow-plus-grip craft.
        public static bool OnMatchesGridRecipe(IPlayer player, GridRecipe recipe, ItemSlot[] ingredients, int gridWidth) {
            //Every grid craft in the game reaches this, so it leads with the cheapest question that rules almost all
            //of them out: a grip craft is generated with the bow as its OUTPUT, so anything producing something else
            //is not one and is answered without reading the grid at all.
            if (ingredients == null || !IsHaftBow(recipe?.Output?.ResolvedItemStack?.Collectible)) {
                return true;
            }

            var gripped = false;
            var sawGrip = false;

            foreach (var slot in ingredients) {
                if (slot?.Itemstack?.Collectible?.Code == null) {
                    continue;
                }

                if (IsHaftBow(slot.Itemstack)) {
                    gripped = slot.Itemstack.HasHandleGripTag();
                } else if (HaftModSystem.Stats.GripParts.ContainsKey(slot.Itemstack.Collectible.Code.Path)) {
                    sawGrip = true;
                }
            }

            return !(gripped && sawGrip);
        }

        //Adds a grip to a bow that already exists, returning whether this craft was one. The bow keeps everything it
        //already had - its limb material above all - because a grip craft is an upgrade of a finished bow rather
        //than the making of a new one, so the output starts as a clone of the input's attributes the way the tool
        //handle's own grip path does.
        //
        //The grip's stat table, texture key and stack attribute are the tool's, reused whole. A wrap around a riser
        //and a wrap around a haft are the same object, so a grip defined for tools is craftable onto a bow without
        //anything bow-specific being added for it.
        private static bool TryApplyGripCraft(ItemSlot[] allInputslots, ItemSlot outputSlot) {
            //The output must be a composed bow before its tier is consulted at all. GetBowTierStats reads
            //Variant["type"], and a bowstave has one too - bowstave-long-raw reports "long" and resolves to the
            //stave tier, which accepts a grip. Asking the tier first therefore let a stave through.
            if (!IsHaftBow(outputSlot.Itemstack)
                    || outputSlot.Itemstack.GetBowTierStats()?.canHaveGrip != true) {
                return false;
            }

            ItemSlot bowSlot = null;
            ItemSlot gripSlot = null;

            foreach (var slot in allInputslots) {
                if (slot.Empty) {
                    continue;
                }

                if (IsHaftBow(slot.Itemstack)) {
                    bowSlot = slot;
                } else if (slot.Itemstack.Collectible?.Code != null
                        && HaftModSystem.Stats.GripParts.ContainsKey(slot.Itemstack.Collectible.Code.Path)) {
                    gripSlot = slot;
                }
            }

            if (bowSlot == null || gripSlot == null) {
                return false;
            }

            //A bow already wearing a grip never reaches here - OnMatchesGridRecipe refuses the craft while the game
            //is still deciding whether the recipe matches, so the grid never produces this call. Left as a guard
            //rather than an assumption, since a caller that is not the grid could still arrive with one.
            if (bowSlot.Itemstack.HasHandleGripTag()) {
                return false;
            }

            var gripPart = HaftModSystem.Stats.GripParts.Get(gripSlot.Itemstack.Collectible.Code.Path);
            var gripStats = gripPart == null ? null : HaftModSystem.Stats.GripStats.Get(gripPart.gripStatTag);
            if (gripStats == null) {
                return false;
            }

            outputSlot.Itemstack.Attributes = bowSlot.Itemstack.Attributes.Clone();
            outputSlot.Itemstack.SetHandleGripTag(gripStats.id);

            ApplyRenderingForLimb(outputSlot.Itemstack);
            return true;
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

            //A bow renders from vanilla's own shape and is left alone here. Only the stave recolours, which is a
            //single-part tree over a single shape.
            //
            //A bow carried a multi-part tree of its own while it was composed from limb, grip, string and arrow
            //part shapes. Those shapes do not exist any more, so the tree is removed rather than merely left
            //unwritten: a bow crafted while they did still carries one, and GenMesh checks multi-part FIRST, so
            //that stale tree would send an existing bow looking for part shapes that were deleted.
            if (IsHaftBow(stack)) {
                stack.RemoveMultiPartRenderTree();
                return;
            }

            //A stave must not carry a multi-part tree either. GetMultiPartRenderTree CREATES the tree as a side
            //effect of reading it, so a single call against a stave is enough to leave an EMPTY one behind - and
            //GenMesh checks multi-part first, finds it empty, and falls through to the untextured item default,
            //ignoring the single-part texture tree written just below. That is a stave rendering as a plain bow.
            //Removing it here also repairs staves crafted while that bug was live.
            stack.RemoveMultiPartRenderTree();
            ApplyLimbTexturesToRenderTree(stack);
        }

        //Whether this is one of Haft's own bows - the item that carries the part axes - rather than a vanilla bow
        //or a stave.
        public static bool IsHaftBow(ItemStack stack) {
            return IsHaftBow(stack?.Collectible);
        }

        //The same question asked of the item type rather than of a stack, for the places that have no stack to ask
        //about - registering which items take a grip, above all.
        //
        //The test is the item's own code, not "declares the BowLimb behavior". A bowstave declares it too, for its
        //own wood axis and colouring, and a stave is emphatically NOT a bow: it is a toolhead that dries into a
        //different item and is then consumed. Treating one as a bow generated grip recipes for staves, which let a
        //grip be wrapped around a stave, vanish when the stave dried into a new item, and survive into the finished
        //bow when it did not.
        //
        //It used to also require the rendering behavior, back when a bow was drawn from its own part shapes. It no
        //longer renders from parts - it uses vanilla's shape until real part models exist - so requiring a
        //rendering behavior to answer a question about STATS would have made every part axis silently inert the
        //moment that behavior came off the itemtype.
        public static bool IsHaftBow(CollectibleObject collectible) {
            return collectible?.Code?.Path?.StartsWith("bowparted") == true;
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
            return (int)Math.Round(durability * HaftPartStatsHelpers.CalculateBowLimbDurability(materialStats, itemstack.GetBowTierStats()));
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

            var bowStats = inSlot.Itemstack.GetBowTierStats();

            //How the bow was built comes first, and is printed before anything can return early: it is the one line
            //that is known for certain from the item alone, so even a bow with no material recorded still says what
            //kind of bow it is rather than reporting Unknown and nothing else.
            if (bowStats != null) {
                dsc.AppendLine(Lang.Get("haftbowbuild", Lang.Get("bowbuild-" + bowStats.id)));
            }

            //A material is recorded when the stave is sawn, so only a limb that was actually crafted has one. A
            //creative-spawned bow never passed through that step and genuinely has no limb material, so it says so
            //rather than printing the numbers for a wood nobody chose.
            //
            //A tier with no wood axis has no species to report and is not missing one. A crude bow is sticks and
            //cordage, so it prints its numbers with no Limb line at all rather than naming the wood standing in for
            //them - the same silence a crude handle keeps about the firewood it was whittled from.
            var hasWoodAxis = inSlot.Itemstack.BowHasWoodAxis();
            if (hasWoodAxis && !inSlot.Itemstack.HasLimbMaterialTag()) {
                dsc.AppendLine(Lang.Get("haftbowlimbunknown"));
                return;
            }

            var materialStats = inSlot.Itemstack.GetLimbMaterialStats();

            if (hasWoodAxis) {
                dsc.AppendLine(Lang.Get("haftbowlimb", Lang.Get("material-" + inSlot.Itemstack.GetLimbMaterialTag())));
            }

            //A material with no flexibility value cannot be a limb at all. That is the state every material a compat
            //mod adds starts in, so it is a normal thing to display rather than an error worth hiding.
            if (!HaftPartStatsHelpers.CanMaterialFormLimb(materialStats)) {
                return;
            }

            //The grip, and what it is worth. A bare riser has an empty langTag and prints nothing rather than a line
            //saying it has no grip, matching how a tool omits a binding it does not have.
            var gripStats = inSlot.Itemstack.GetBowGripStats();
            if (gripStats != null && gripStats.langTag != "") {
                dsc.AppendLine(Lang.Get("haftbowgrip", Lang.Get(gripStats.langTag)));
                if (gripStats.accuracyBonus > 0.0f) {
                    dsc.AppendLine(Lang.Get("haftbowaccuracy",
                        StringHelpers.ColorForMultiplier(1.0f + gripStats.accuracyBonus),
                        float.Truncate(gripStats.accuracyBonus * 100) / 100));
                }

                //Shown as a percent because that is literally what it is - the share of a disturbance the grip
                //absorbs - where the line above is an amount added to a stat.
                if (gripStats.steadyBonus > 0.0f) {
                    dsc.AppendLine(Lang.Get("haftbowsteady",
                        StringHelpers.ColorForMultiplier(1.0f + gripStats.steadyBonus),
                        float.Truncate(gripStats.steadyBonus * 100)));
                }
            }

            var drawWeight = HaftPartStatsHelpers.CalculateBowDrawWeight(materialStats, bowStats);
            var drawSpeed = HaftPartStatsHelpers.CalculateBowDrawSpeed(materialStats);
            var limbDurability = HaftPartStatsHelpers.CalculateBowLimbDurability(materialStats, bowStats);

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
            var treatmentStats = inSlot.Itemstack.GetTreatmentStatsOrDefault();
            var refundChance = HaftPartStatsHelpers.CalculateBowLimbRefundChance(materialStats, treatmentStats);

            //Only shown when the limb actually has one. A bow at oak's baseline springback refunds nothing, and a
            //zero here would read as a broken stat rather than as the baseline it is.
            if (refundChance > 0.0f) {
                dsc.AppendLine(Lang.Get("haftbowlimbrefund", StringHelpers.ColorForBonus(refundChance), Math.Round(refundChance * 100)));
            }
        }
    }
}
