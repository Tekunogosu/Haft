using System;
using System.Reflection;
using HarmonyLib;
using Haft.Client.Behaviors;
using Haft.Config;
using Haft.ToolTinkering.Behaviors;
using Haft.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Haft.ToolTinkering {

    //Makes the limb material a bow was built from actually change how it shoots. Everything the wood axis records -
    //draw power, draw speed, limb life - was carried and displayed before this and consumed by nothing, so a
    //purpleheart bow shot exactly like an oak one.
    //
    //None of this is a transpiler. Every value that needed changing turned out to sit behind a public settable
    //property or a virtual call, so the three hooks below are plain prefixes and postfixes against seams the engine
    //already offers.
    public static class BowStatHelper {

        //The minimum seconds of draw vanilla requires before a shot leaves the bow at all, from ItemBow's
        //OnHeldInteractStop. Mirrored rather than read because it is a literal in the method being patched.
        public const float VanillaMinimumDrawSeconds = 0.65f;

        //What Haft charges instead: every bow draws 10% slower than vanilla, before the limb's own multiplier.
        //
        //Applied here rather than by raising each material's drawSpeedBonus so that oak stays exactly 1.00. That
        //field means "how this limb compares to oak", and folding a global tuning pass into all 31 material values
        //would leave the baseline material reading 1.10 against itself. A change that applies to every bow equally
        //belongs to the bow, not to the wood.
        public const float HaftDrawTimeScale = 1.10f;

        //The draw a shot has to clear, with Haft's slower pacing applied. The limb's own multiplier scales this.
        public const float BaseDrawSeconds = VanillaMinimumDrawSeconds * HaftDrawTimeScale;

        //Reads the limb off whichever stack is the bow, and answers null for anything that is not a Haft bow with a
        //material recorded on it. Every hook below leads with this and leaves vanilla untouched on null, which is
        //what keeps a creative-spawned bow, a save from before the wood axis existed, and an unrelated projectile
        //like a thrown spear all behaving exactly as they did.
        public static MaterialStatDefines ResolveLimbStats(ItemStack bowStack) {
            var materialStats = bowStack?.GetLimbMaterialStats();
            return HaftPartStatsHelpers.CanMaterialFormLimb(materialStats) ? materialStats : null;
        }

        //The limb's draw time for a bow held in this slot, or 0 when the slot holds anything else. Both visual
        //systems below ask this so they cannot disagree with the gate the shot actually clears.
        public static float ResolveDrawTimeMultiplier(ItemStack bowStack) {
            var materialStats = ResolveLimbStats(bowStack);
            if (materialStats == null) {
                return 0.0f;
            }

            var drawTime = HaftPartStatsHelpers.CalculateBowDrawSpeed(materialStats);
            if (drawTime <= 0.0f) {
                return 0.0f;
            }

            //Floored in SECONDS rather than as a multiplier: the floor states how long a motion needs in order to be
            //visible, which is a fixed duration and not a ratio of anything.
            return Math.Max(drawTime * HaftDrawTimeScale, MinimumDrawSeconds / VanillaMinimumDrawSeconds);
        }

        //The seconds a bow actually needs held before it will fire, floor included.
        public static float ResolveDrawSeconds(ItemStack bowStack) {
            var multiplier = ResolveDrawTimeMultiplier(bowStack);
            return multiplier > 0.0f ? VanillaMinimumDrawSeconds * multiplier : 0.0f;
        }

        //How far the aim animation's playback speed is allowed to move. Draw time spans 0.24s to 1.68s across the
        //Vanilla's fully-drawn model stage arrives at 0.75s (Ceiling(secondsUsed * 4) hitting 3) while the shot gate
        //is 0.65s. This is that ratio, applied so the two land together instead of the model leading the shot.
        public const float RenderStageToGateRatio = 0.75f / VanillaMinimumDrawSeconds;

        //The shortest a bow may take to draw, whatever its limb. Below roughly this the nocking has no time to read
        //as a motion: the model snaps through its four stages and the arm barely moves, so a very light bow looks
        //broken rather than fast. Kapok, redwood and baldcypress sit under it and are raised to it; every other
        //wood keeps its own time.
        public const float MinimumDrawSeconds = 0.40f;

        //The value vanilla's stage arithmetic reaches at full draw. Ceiling(secondsUsed * 4) clamped to 3 means the
        //top stage is due once the value passes 0.75, so that is what the prefix maps full draw onto.
        public const float RenderStageFullDraw = 0.75f;

        //How many drawn poses the bow steps through, matching the alternates declared in bow-draw-tweens.json and
        //the files scripts/gen-bow-charge-tweens.py writes. Index 0 is the undrawn base shape, so the poses run 1
        //to this number. Changing it means regenerating both.
        //MUST match both the alternates declared in bow-draw-tweens.json and the files
        //scripts/gen-bow-charge-tweens.py writes (STEPS_BETWEEN * 2 + 3). All three drifted apart once already:
        //the generator was lowered to 11 poses while the patch still declared 17 alternates and this still said 17,
        //so the bow selected shapes that did not exist and rendered the missing-asset placeholder.
        public const int DrawPoseCount = 11;

        //How the poses are distributed across the draw. The exponent biases them later, matching an arm animation
        //whose own keyframes load late, and 1.1 is deliberately close to linear: the steeper p^2 used when there
        //were only three stages held the first pose for a third of the draw, which on a slow limb is half a second
        //of a bow that has visibly not moved, followed by a rush. With this many poses the bias can be slight and
        //still read correctly.
        public const float DrawPoseCurve = 1.1f;

        //How long the aim animations run unscaled. All four are 16 frames (quantityframes) and RunningAnimation
        //advances 30 frames a second, so the last frame lands at 15/30 = 0.50s.
        //
        //This number is why the animation stopped and snapped into place on a heavy bow. The animations declare
        //onAnimationEnd "Hold", so the pose freezes on the final frame once the motion finishes - and driving the
        //speed from 1/drawTime made an ebony bow's motion end at 1.00s while the shot was not legal until 1.68s,
        //leaving two thirds of a second of held pose. Deriving the speed from this duration against the real draw
        //time instead puts the arm at full draw exactly as the shot becomes legal.
        public const float AimAnimationNaturalSeconds = 15f / 30f;

        //Rolls whether this shot's wear is handed straight back to the limb. Server-side only: the client holding a
        //different opinion about whether a bow took damage is a desync, and the server owns durability everywhere
        //else in the mod for the same reason.
        public static bool ShouldRefundShotWear(IWorldAccessor world, ItemStack bowStack) {
            if (world == null || !world.Side.IsServer()) {
                return false;
            }

            var materialStats = ResolveLimbStats(bowStack);
            if (materialStats == null) {
                return false;
            }

            var treatmentStats = HaftModSystem.Stats.TreatmentStats.Get(
                bowStack.HasHandleTreatmentTag() ? bowStack.GetHandleTreatmentTag() : HaftConstants.DefaultTreatmentTag);
            var refundChance = HaftPartStatsHelpers.CalculateBowLimbRefundChance(materialStats, treatmentStats);

            return refundChance > 0.0f && world.Rand.NextDouble() < refundChance;
        }
    }

    //Draw power and velocity, applied to the arrow the moment before it is spawned.
    //
    //PreInitialize is the seam because of where it sits in ItemBow.OnHeldInteractStop: Damage, WeaponStack,
    //ProjectileStack and Pos.Motion are ALL assigned before it is called, and SpawnPriorityEntity comes after, so
    //this runs with every value present and nothing yet in the world. The damage sum itself is a local in that
    //method and the entity is constructed there too, which is what rules out reaching it from a postfix on the bow.
    [HarmonyPatch(typeof(EntityProjectile))]
    [HarmonyPatchCategory(HaftModSystem.ToolTinkeringBowPatchCategory)]
    public class BowProjectilePatches {

        //Targeted by its FULLY QUALIFIED name, which is not a style choice. EntityProjectile implements
        //PreInitialize explicitly - "void IProjectile.PreInitialize()" - so the method is private and final, and its
        //real name carries the interface prefix. A plain [HarmonyPatch("PreInitialize")] does NOT find it: that
        //resolves to the empty virtual inherited from EntityProjectileBase, which an arrow never dispatches to, and
        //the patch would silently do nothing. Verified by reflection against the shipped assembly rather than by
        //reading the decompiler output, which renders the explicit implementation without making either fact
        //visible.
        [HarmonyPostfix]
        [HarmonyPatch("Vintagestory.API.Common.Entities.IProjectile.PreInitialize")]
        private static void PreInitializePostfix(EntityProjectile __instance) {
            var materialStats = BowStatHelper.ResolveLimbStats(__instance?.WeaponStack);
            if (materialStats == null) {
                return; //Not a Haft bow's arrow. Thrown spears reach this too and must stay vanilla.
            }

            var damageFactor = HaftPartStatsHelpers.CalculateBowDamageFactor(materialStats);
            var velocityFactor = HaftPartStatsHelpers.CalculateBowVelocityFactor(materialStats);

            //Damage is scaled on the combined bow-plus-arrow total vanilla already summed, deliberately: the limb
            //drives whatever arrow is nocked, so a better bow improves a good arrow rather than being averaged
            //against it.
            __instance.Damage *= damageFactor;

            //Motion is already aimed and already carries the entity's bowDrawingStrength, so scaling it in place
            //keeps the player's aim and stat bonuses intact and changes only how fast the arrow leaves.
            __instance.Pos.Motion.Mul(velocityFactor);

            if (HaftModSystem.Config.DebugMessages) {
                HaftModSystem.Logger.Debug("[BowStats] limb " + materialStats.id + ": damage x" + damageFactor + ", velocity x" + velocityFactor + ", final damage " + __instance.Damage);
            }
        }
    }

    //The player's aim animation, retimed to the limb.
    //
    //Patched on the AnimationMetaData overload rather than the string one because that is where BOTH paths land:
    //StartAnimation(string) looks the code up in AnimationsByMetaCode and calls straight through to this.
    //
    //The substituted metadata is a CLONE, and that is not tidiness. AnimationsByMetaCode hands out references owned
    //by the entity TYPE, shared by every entity of that type, and StartAnimation stores whatever reference it is
    //given into ActiveAnimationsByAnimCode. Setting AnimationSpeed on the original would change the aim speed for
    //every player on the server and keep it changed after the shot - a bug that would look like an unrelated
    //desync days later. The clone is per-shot and discarded with the animation.
    [HarmonyPatch(typeof(AnimationManager))]
    [HarmonyPatchCategory(HaftModSystem.ToolTinkeringBowPatchCategory)]
    public class BowAimAnimationPatches {

        [HarmonyPrefix]
        [HarmonyPatch(nameof(AnimationManager.StartAnimation), typeof(AnimationMetaData))]
        private static void StartAnimationPrefix(ref AnimationMetaData animdata, Entity ___entity) {
            if (animdata?.Code == null || ___entity is not EntityAgent agent) {
                return;
            }

            //Only the bow aim animations, and only the ones this bow actually declares. Reading the attribute off
            //the held bow rather than matching on a name prefix means a modded bow with its own aim animation is
            //retimed too, and a non-bow animation that happens to be called "bowsomething" is not.
            var slot = agent.RightHandItemSlot;
            var bowStack = slot?.Itemstack;
            if (bowStack?.Collectible is not ItemBow) {
                return;
            }

            var aimAnimation = bowStack.Collectible.Attributes?["aimAnimation"].AsString(null);
            if (aimAnimation == null || !animdata.Code.Equals(aimAnimation, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            var drawTime = BowStatHelper.ResolveDrawTimeMultiplier(bowStack);
            if (drawTime <= 0.0f) {
                return;
            }

            //Speed is the animation's own length over the draw it has to cover, NOT the inverse of the draw
            //multiplier. The two differ because the animation runs 0.50s while vanilla's gate is 0.65s, so the
            //inverse left every bow's motion finishing early and holding - barely visible on oak, two thirds of a
            //second on ebony. This form makes the motion span the draw exactly, for every wood.
            var drawSeconds = BowStatHelper.VanillaMinimumDrawSeconds * drawTime;
            var speed = BowStatHelper.AimAnimationNaturalSeconds / drawSeconds;

            var retimed = animdata.Clone();
            retimed.AnimationSpeed = animdata.AnimationSpeed * speed;

            //Ease-in and ease-out are rates applied to a dt that RunningAnimation has ALREADY multiplied by the
            //animation speed, so slowing the animation slows the pose blend by the same factor. On an ebony bow at
            //0.30x that stretches a blend meant to take about a tenth of a second into a third of a second: the arm
            //drifts into the pose and then the last frames arrive all at once, which is the snap that survived the
            //timing fix. Dividing the rates by the speed holds the blend at its real-time duration whatever the
            //animation is doing.
            retimed.EaseInSpeed = animdata.EaseInSpeed / speed;
            retimed.EaseOutSpeed = animdata.EaseOutSpeed / speed;

            animdata = retimed;
        }
    }

    //The aiming reticle, retimed to the limb.
    //
    //BaseAimingAccuracy converges the reticle on SecondsSinceAimStart * rangedWeaponsSpeed * rangedWeaponsSpeedMul
    //* 1.7, so it tightens on a fixed schedule that knows nothing about how long this bow takes to draw. A heavy
    //bow therefore showed a tight reticle long before it could fire, and a light one fired while the reticle was
    //still wide - the same disagreement the model and the animation had.
    //
    //Vanilla already has the right lever for this: rangedWeaponsSpeedMul, read per item. It cannot carry the answer
    //here because ItemStack.ItemAttributes returns Collectible.Attributes, which is shared by every stack of a type
    //- a JSON value would be the same for an ebony bow and a kapok one. The multiplier is applied here instead, off
    //the stack's own limb material.
    [HarmonyPatch(typeof(BaseAimingAccuracy))]
    [HarmonyPatchCategory(HaftModSystem.ToolTinkeringBowPatchCategory)]
    public class BowAimingAccuracyPatches {

        [HarmonyPostfix]
        [HarmonyPatch(nameof(BaseAimingAccuracy.Update))]
        private static void UpdatePostfix(BaseAimingAccuracy __instance, EntityAgent ___entity, ref float accuracy) {
            var bowStack = ___entity?.RightHandItemSlot?.Itemstack;
            if (bowStack?.Collectible is not ItemBow) {
                return;
            }

            var drawSeconds = BowStatHelper.ResolveDrawSeconds(bowStack);
            if (drawSeconds <= 0.0f) {
                return;
            }

            //Recomputed rather than scaled: vanilla clamps the value before this postfix sees it, so multiplying the
            //result would compress an already-clamped number instead of changing when the clamp is reached.
            var ceiling = accuracy;
            var progress = GameMath.Clamp(__instance.SecondsSinceAimStart / drawSeconds, 0.0f, 1.0f);

            accuracy = Math.Min(accuracy, ceiling * progress);
        }
    }

    [HarmonyPatch(typeof(ItemBow))]
    [HarmonyPatchCategory(HaftModSystem.ToolTinkeringBowPatchCategory)]
    public class BowDrawPatches {

        //Draw speed, applied by moving the bar the shot has to clear rather than the animation.
        //
        //secondsUsed is compared against a flat 0.65 before a shot is allowed out, and that gate is the only thing
        //in the method that reads it - the 0-3 clamp in OnHeldInteractStep is renderVariant, the visual charge
        //stage, and patching it would change how the bow LOOKS while drawing and nothing about when it fires.
        //
        //Scaling the value passed in is what lets a quick limb fire sooner: kapok at +0.07 reaches the gate in
        //about 0.61s, ebony at -0.04 needs about 0.68s. speedBonus is a small band by design, so this is a nudge
        //rather than a rebalance - which is correct, since the draw speed the player feels is mostly the animation.
        [HarmonyPrefix]
        [HarmonyPatch(nameof(ItemBow.OnHeldInteractStop))]
        private static void OnHeldInteractStopPrefix(ref float secondsUsed, ItemSlot slot) {
            var materialStats = BowStatHelper.ResolveLimbStats(slot?.Itemstack);
            if (materialStats == null) {
                return;
            }

            var drawTime = HaftPartStatsHelpers.CalculateBowDrawSpeed(materialStats);
            if (drawTime <= 0.0f) {
                return;
            }

            //drawTime is a multiplier on how long the limb needs, so it DIVIDES the time the player has put in: a
            //limb that takes 1.7x as long reaches the gate when 1.7x the real seconds have passed. Dividing rather
            //than multiplying is the whole difference between a heavy limb being slow and it being fast.
            //
            //HaftDrawTimeScale divides for the same reason: it lengthens every draw, so it lengthens this one too.
            secondsUsed /= drawTime * BowStatHelper.HaftDrawTimeScale;
        }

        //The bow model's four draw stages, retimed to the limb.
        //
        //Vanilla derives renderVariant from Math.Ceiling(secondsUsed * 4f) clamped to 0-3, so the fully-drawn model
        //appears at 0.75s for every bow. Once the shot gate moved off 0.65s that became a lie the player reads
        //directly: an ebony bow showed fully drawn at 0.75s and refused to fire for another 0.93s, and a kapok bow
        //fired while the model was still mid-draw. Scaling secondsUsed the same way the gate does puts stage 3 on
        //the exact moment the shot becomes legal, for every wood.
        //
        //This is the clamp an earlier pass deliberately left alone as "visual only". That was right for draw SPEED,
        //which belongs on the gate - and it is exactly why the visuals then disagreed with it.
        //Vanilla clamps the stage to 0-3 inside the method, so the extra draw poses cannot be reached by feeding it
        //a different secondsUsed - the prefix below still shapes WHEN each stage is due, and this postfix writes the
        //finer stage over the top once vanilla has finished.
        //
        //Haft declares nine shape alternates, generated by scripts/gen-bow-charge-tweens.py, so the drawn bow steps
        //through nine poses instead of three. BakedAlternates includes the base shape at index 0 and the declared
        //alternates from 1, which is why the undrawn bow is 0 and the draw runs 1 to 9.
        [HarmonyPostfix]
        [HarmonyPatch(nameof(ItemBow.OnHeldInteractStep))]
        private static void OnHeldInteractStepPostfix(float secondsUsed, ItemSlot slot, EntityAgent byEntity) {
            var stack = slot?.Itemstack;
            if (stack == null) {
                return;
            }

            var drawTime = BowStatHelper.ResolveDrawTimeMultiplier(stack);
            if (drawTime <= 0.0f) {
                return;
            }

            //secondsUsed arrives already divided by the prefix, so it is progress on vanilla's own scale. Undo that
            //to get the fraction of this bow's draw, then square it for the same reason the stage map does: the
            //authored motion loads late, and a linear pose sequence runs ahead of the arm.
            var progress = GameMath.Clamp(secondsUsed / BowStatHelper.RenderStageFullDraw, 0.0f, 1.0f);

            //The prefix already squared the progress for vanilla's coarse three stages; undo that before applying
            //the gentler curve these finer poses want, or the two compound into the bunching this replaced.
            var linear = (float)Math.Sqrt(progress);
            var pose = (int)Math.Ceiling(Math.Pow(linear, BowStatHelper.DrawPoseCurve) * BowStatHelper.DrawPoseCount);
            pose = GameMath.Clamp(pose, 0, BowStatHelper.DrawPoseCount);

            var previous = stack.Attributes.GetInt("renderVariant", 0);
            if (previous == pose) {
                return;
            }

            if (byEntity.World.Side == EnumAppSide.Client) {
                stack.TempAttributes.SetInt("renderVariant", pose);
            }

            stack.Attributes.SetInt("renderVariant", pose);

            //A composed bow renders from its part tree rather than from a shape alternate, so the tree has to be
            //repointed at this pose's part shapes. A bow without the rendering behavior ignores this and is drawn
            //by the alternate the attribute above already selected.
            if (CollectibleBehaviorBowLimb.IsComposedFromParts(stack)) {
                CollectibleBehaviorBowLimb.ApplyBowPartRenderTree(stack, pose);
            }

            //Vanilla broadcasts the slot when its own stage changes; the finer stages change more often and need the
            //same broadcast or other players see the bow lag behind its owner.
            if (byEntity is EntityPlayer player) {
                player.Player?.InventoryManager.BroadcastHotbarSlot();
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(ItemBow.OnHeldInteractStep))]
        private static void OnHeldInteractStepPrefix(ref float secondsUsed, ItemSlot slot) {
            var drawTime = BowStatHelper.ResolveDrawTimeMultiplier(slot?.Itemstack);
            if (drawTime <= 0.0f) {
                return;
            }

            //Mapped onto the draw as a fraction, not scaled by it.
            //
            //Vanilla derives the stage from Ceiling(secondsUsed * 4) clamped 0-3, so stage 3 TRIGGERS as soon as
            //the value passes 0.5 and merely stays there until 0.75. Scaling secondsUsed preserves that shape,
            //which means the fully-drawn model appears at two thirds of the draw however the draw is timed - half
            //a second early on an ebony bow, and the arm is still visibly pulling while the bow reads as ready.
            //
            //Squaring the progress before feeding the ceiling puts stage 2 at 58% and stage 3 at 82% of the draw,
            //against 33% and 67% for a linear map. Linear is what the earlier ratio produced, and it is why a heavy
            //bow still looked ready well before it was: that arithmetic checked where the ceiling TOPPED OUT rather
            //than where it first TRIGGERED, which is the number the player actually sees.
            //
            //The curve also matches how the animation itself is authored. bowaimlong's keyframes are at 0, 6, 10,
            //13, 15 - the raise takes 40% of the motion and the draw-back compresses into progressively shorter
            //segments, so the pull loads late. A linear stage map ran the model ahead of that motion; squaring puts
            //the two back on the same shape.
            var drawSeconds = BowStatHelper.VanillaMinimumDrawSeconds * drawTime;
            var progress = GameMath.Clamp(secondsUsed / drawSeconds, 0.0f, 1.0f);

            secondsUsed = progress * progress * 0.75f;
        }

        //Vanilla sets renderVariant to 1 the instant the button goes down, before any draw time has passed, and
        //OnHeldInteractStep then recomputes it from secondsUsed starting near zero. At vanilla's pacing that is
        //invisible - stage 1 is reached almost immediately anyway - but once a slow limb stretches the draw, the
        //model jumps to stage 1, falls back to 0 on the next tick, and climbs again. That flicker is the bow
        //appearing to snap as it comes up.
        //
        //Postfix rather than prefix: vanilla has to finish its own work, including the aiming attributes and the
        //sound, and only the stage it left behind is corrected.
        [HarmonyPostfix]
        [HarmonyPatch(nameof(ItemBow.OnHeldInteractStart))]
        private static void OnHeldInteractStartPostfix(ItemSlot slot, EntityAgent byEntity) {
            var stack = slot?.Itemstack;
            if (stack == null || BowStatHelper.ResolveDrawTimeMultiplier(stack) <= 0.0f) {
                return;
            }

            //Only when the bow actually started aiming. OnHeldInteractStart returns early without setting the
            //attribute when there is no arrow, and zeroing it then would fight whatever else holds the slot.
            if (byEntity?.Attributes?.GetInt("aiming", 0) != 1) {
                return;
            }

            if (byEntity.World.Side == EnumAppSide.Client) {
                stack.TempAttributes.SetInt("renderVariant", 0);
            }

            stack.Attributes.SetInt("renderVariant", 0);
        }

        //The per-shot half of limb life. Vanilla spends exactly one durability per shot, unconditionally, at the end
        //of OnHeldInteractStop; this hands that point back when the limb's springback wins the roll.
        //
        //Written as a refund rather than as a patch on the DamageItem call because the wear has to have happened for
        //every other system that watches it - a tinkered-tool break check, a compat mod counting durability - to see
        //a consistent story. The refund is a second, separate write on top, not a suppression of the first.
        [HarmonyPostfix]
        [HarmonyPatch(nameof(ItemBow.OnHeldInteractStop))]
        private static void OnHeldInteractStopPostfix(ItemSlot slot, EntityAgent byEntity) {
            var stack = slot?.Itemstack;
            if (stack == null || byEntity?.World == null) {
                return;
            }

            if (!BowStatHelper.ShouldRefundShotWear(byEntity.World, stack)) {
                return;
            }

            var currentDurability = stack.Attributes.GetInt("durability", stack.Collectible.GetMaxDurability(stack));
            var maxDurability = stack.Collectible.GetMaxDurability(stack);

            //Never past full: the shot that was refunded might not have been taken at all - the method returns early
            //on a cancelled aim or too short a draw - and this must not turn those paths into free repairs.
            if (currentDurability >= maxDurability) {
                return;
            }

            stack.Attributes.SetInt("durability", currentDurability + 1);
            slot.MarkDirty();

            if (HaftModSystem.Config.DebugMessages) {
                HaftModSystem.Logger.Debug("[BowStats] limb shrugged off a shot's wear: durability back to " + (currentDurability + 1) + " / " + maxDurability);
            }
        }
    }
}
