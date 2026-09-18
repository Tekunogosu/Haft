using System;
using System.Collections.Generic;
using Haft.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Util;

namespace Haft.Config {

    public class HaftPartStats {

        public bool EnableEdits = false;

        public Dictionary<string, HandlePartDefines> BaseHandleParts = new() { }; //These all now get populated by Json Config Files for everything! Better for compat and easy content modder integration!
        public Dictionary<string, GripPartDefines> GripParts = new() { }; //I'm learning!
        public Dictionary<string, TreatmentPartDefines> TreatmentParts = new() { };
        public Dictionary<string, BindingPartDefines> BindingParts = new() { };

        public Dictionary<string, HandleStatDefines> BaseHandleStats = new() { };
        public Dictionary<string, GripStatDefines> GripStats = new() { };
        public Dictionary<string, TreatmentStatDefines> TreatmentStats = new() { };
        public Dictionary<string, BindingStatDefines> BindingStats = new() { };
        public Dictionary<string, MaterialStatDefines> MaterialStats = new() { };
        public Dictionary<string, BowStatDefines> BowStats = new() { };
    }

    //The four stat blocks a finished tool's numbers are calculated from. Grouped because they are always wanted
    //together and always resolved the same way: read the tag the stack carries, fall back to the default when it has
    //none. Crafting a tool and rebuilding one that lost its stats both go through this, so the two cannot drift.
    public class HandleStatBundle {
        public HandleStatDefines Handle;
        public GripStatDefines Grip;
        public TreatmentStatDefines Treatment;
        public BindingStatDefines Binding;
        public MaterialStatDefines Material;
    }

    public static class HaftPartStatsHelpers {

        //The four values a finished tool inherits from its handle, treatment, grip and binding. Both the crafting
        //path and the repair path that rebuilds a tool missing its stats have to arrive at the same numbers, so the
        //math lives here once rather than in each of them - tuning a bonus in one place and not the other would
        //otherwise ship a game where a crafted handle and a repaired one wear out at different rates.
        //
        //Returned as float because callers scale by a remaining-HP percent before rounding; returning int here
        //would round twice and lose durability on every repair.

        //Starts from a flat base shared by every handle and takes each bonus in turn - the handle's own, the treatment
        //on it, then the binding holding it - each applied to the running total rather than to the base, so they
        //compound rather than simply adding up.
        //The material scales the handle's own factor rather than the finished total, so a dense material and a good
        //treatment compound with each other instead of being added on at the end independently.
        //
        //The base is deliberately NOT the tool's own durability. A handle is a bar of steel or a length of oak and
        //knows nothing about the head fixed to it: scaling it by the head's metal said that the same steel handle was
        //weak on a copper tool and strong on a steel one, which is backwards - the whole appeal of a good handle is
        //that it outlives the heads it carries. Each part now derives its durability from what it is actually made of.
        public static float CalculateHandleDurability(HandleStatDefines handleStats, TreatmentStatDefines treatmentStats, BindingStatDefines bindingStats, MaterialStatDefines woodStats) {
            var handleDur = HaftConstants.PartDurabilityBase * handleStats.baseHPfactor * woodStats.hardness;
            handleDur += handleDur * handleStats.selfHPBonus;
            handleDur += handleDur * treatmentStats.handleHPbonus;
            handleDur += handleDur * bindingStats.handleHPBonus;
            return handleDur;
        }

        //The head's counterpart to the handle above, and deliberately the simpler of the two. A head is a single
        //piece of one material with nothing fixed to it, so it has no tier factor, no treatment and no binding to
        //take a bonus from - what it is made of is the whole story.
        //
        //The base is NOT the tool's vanilla durability, for the reason the handle gives above and one of its own:
        //vanilla's base is already scaled by the metal's tier, so multiplying it by hardness would count the material
        //twice and widen the copper-to-steel gap far past what either table intends. Reading hardness alone leaves
        //one source for the material axis.
        //
        //Hardness, not density: the two order the metals almost oppositely, and gold is among the densest and the
        //softest. A head derived from density would make a gold head one of the toughest in the game.
        public static float CalculateHeadDurability(MaterialStatDefines materialStats) {
            return HaftConstants.PartDurabilityBase * materialStats.hardness;
        }

        //How much durability a head carries into the tool being built around it. The head keeps the absolute damage
        //it has taken rather than a fraction of it: a head is the same lump of metal before and after, so what it
        //carries is a count of hits taken, not a proportion of whatever it is fitted to next.
        //
        //recordedDurability is what the head has left, and hasRecord says whether it has ever been fitted to anything
        //- a head straight from the anvil has no record and starts full, which is not the same as one worn down to
        //nothing. The clamp is what a rebuilt fraction used to violate: a head handed back from a broken tool can
        //never return with more than a new one has.
        public static int TransferHeadDurability(int maxDurability, int recordedDurability, bool hasRecord) {
            if (!hasRecord) {
                return maxDurability;
            }

            return Math.Clamp(recordedDurability, 0, maxDurability);
        }

        //The binding has fewer terms than the handle: its own factor and bonus, plus whatever support the handle
        //lends it. Nothing a treatment does reaches the binding.
        //
        //Shares the handle's flat base, and for the same reason: a twine wrap is a twine wrap whatever it is holding.
        //
        //The material only reaches a binding that is nailed on - isMetal already means exactly that. A wrap of rope or
        //twine is tightened around the handle rather than driven into it, so how dense the handle is makes no
        //difference to how well it holds.
        public static float CalculateBindingDurability(HandleStatDefines handleStats, BindingStatDefines bindingStats, MaterialStatDefines woodStats) {
            var bindingDur = HaftConstants.PartDurabilityBase * bindingStats.baseHPfactor;
            bindingDur += bindingDur * bindingStats.selfHPBonus;
            bindingDur += bindingDur * handleStats.bindingHPBonus;
            if (bindingStats.isMetal) {
                bindingDur += bindingDur * woodStats.nailBindingBonus;
            }
            return bindingDur;
        }

        //A better handle and a better grip both make the tool quicker to swing, and what the handle is MADE of has a
        //say too. All three stack additively, so a material penalty can be offset by a good grip rather than being
        //an inescapable tax on using metal at all.
        public static float CalculateSpeedBonus(HandleStatDefines handleStats, GripStatDefines gripStats, MaterialStatDefines materialStats = null) {
            var speed = handleStats.speedBonus + gripStats.speedBonus;

            if (materialStats != null) {
                speed += materialStats.speedBonus;
            }

            return speed;
        }

        //The chance the handle takes damage at all. Two independent things can prevent a knock landing: a grip that
        //stops the hand slipping, and a treated surface that shrugs the wear off. They multiply rather than add, so
        //each one removes a share of what still gets through and the pair can never reach a guaranteed save.
        public static float CalculateGripChanceToDamage(GripStatDefines gripStats, TreatmentStatDefines treatmentStats = null) {
            var chance = gripStats.chanceToDamage;

            if (treatmentStats != null && treatmentStats.chanceToDamageReduction > 0.0f) {
                chance *= (1.0f - treatmentStats.chanceToDamageReduction);
            }

            return chance;
        }

        // -- Bow limbs --
        //
        //A bow reads the SAME material table a handle does, deriving its three behaviours from density,
        //flexibility and speedBonus rather than carrying a parallel set of bow-only fields. That is the whole reason
        //flexibility was added as a second axis instead of bow numbers being hand-written per species: a material
        //that gets a handle value automatically gets a bow value, including every material a compat mod adds.
        //
        //Why two fields and not one. Stiffness alone would make the densest wood the best bow, which is backwards -
        //ebony is the hardest wood in the table and snaps. Springback alone would ignore that a limb has to be heavy
        //enough to store energy. Bow behaviour is the RATIO between them, which is why each helper below reads both.
        //
        //Where the numbers land, oak = 1.00 on both axes. Draw speed is a multiplier on the time a shot needs, so
        //below 1.00 is quicker:
        //
        //  material      density  flex   draw weight  draw speed  limb dur   reads as
        //  larch         0.92   1.12      1.03        0.75       1.12     light and springy, the best all-round
        //  birch         0.96   1.13      1.08        1.00       1.13     fast, forgiving
        //  oak           1.00   1.00      1.00        1.00       1.00     the reference
        //  maple         0.98   1.02      1.00        1.12       1.02     baseline recurve wood
        //  acacia        1.02   1.15      1.17        1.41       1.15     high power, demanding draw
        //  purpleheart   1.07   1.65      1.77        1.99       1.65     siege bow: max power, slow
        //  ebony         1.15   1.33      1.53        2.35       1.33     hits hard, heavy to draw
        //  walnut        0.96   0.94      0.90        0.84       0.94     low power, easy draw
        //  pine          0.87   0.73      0.64        0.57       0.73     starter
        //  kapok         0.68   0.30      0.20        0.33       0.30     barely a bow
        //  steel         2.27   0.85      1.93        4.22       0.85     hits hardest of anything, brutal to draw
        //  iron          2.28   0.70      1.60        2.66       0.70     heavy and stiff, poor springback
        //  gold          3.12   0.10      0.31        0.29       0.10     dense and dead - the softest heavy metal
        //
        //Gold is why density and hardness are separate fields. It is the densest material in the table and among the
        //softest: heavy enough to throw an arrow and far too dead to return the energy, so it reads as a poor bow
        //from the physics rather than from a hand-written exception. A single field named for mass but holding
        //hardness rated it as the worst limb for the right reason by accident, and rated lead above it.
        //
        //Ebony and purpleheart are the two rarest woods and read as strong, slow, expensive bows rather than as a
        //trap. An earlier design made ebony a trap on the theory that the hardest wood is brittle and shoots badly;
        //the published data does not support that - ebony's strain to failure (MOR/MOE) is the highest in this
        //table, above oak, maple and birch, and it takes steam bending well. Neither wood has documented use as a
        //real bow limb, so what they get here is a game niche, not a claim about bowyery.
        //
        //Draw weight is deliberately NOT capped or curved, even though steel reaches 3.87 against oak's 1.00. The
        //cost is already paid on the other axis: a high draw weight comes with a low draw speed, so the extreme end
        //of the table is a siege bow - it may drop a moose in one or two shots, and the moose may reach the player
        //before a second arrow is nocked. That is power traded against time-to-second-shot under threat, a decision
        //made in the moment rather than two numbers compared on a table, and capping it would flatten exactly the
        //extreme that makes the choice worth making.
        //
        //Metals are given limb values even where a wood outperforms them, for parity: a material with a handle value
        //should have a bow value, so nothing reads as arbitrarily missing.

        //Draw weight - how much energy the limb stores, and so how hard the arrow hits.
        //
        //The two axes MULTIPLY rather than add. A limb needs both mass to store energy and stiffness to return it;
        //a material short on either is a poor bow however good the other is, and adding them would let a very dense
        //but dead material (lead) pass as usable on density alone.
        //
        //The bow's own tier multiplies in on top, the way a handle's tier multiplies against its wood. A tier of null
        //means the caller had no bow to ask about - an unrelated projectile, or a vanilla bow the mod does not build -
        //and leaves the material's own number untouched rather than zeroing it.
        public static float CalculateBowDrawWeight(MaterialStatDefines materialStats, BowStatDefines bowStats = null) {
            if (!CanMaterialFormLimb(materialStats)) {
                return 0.0f;
            }

            return materialStats.density * materialStats.flexibility * (bowStats?.drawWeightFactor ?? 1.0f);
        }

        //How long the limb takes to come to full draw, as a multiplier on the time a shot needs. Oak is 1.00.
        //
        //Read from the material's own drawSpeedBonus rather than from speedBonus, which is the handle's swing speed
        //and a different physical fact - see the field comment on drawSpeedBonus. Reusing speedBonus put the entire
        //table inside a 70ms band, which meant the axis that is supposed to pay for high draw weight cost nothing.
        //
        //A material with no draw data falls back to oak's 1.00 rather than to zero: this is a multiplier on time,
        //and zero would mean a bow that fires the instant it is raised.
        public static float CalculateBowDrawSpeed(MaterialStatDefines materialStats) {
            if (!CanMaterialFormLimb(materialStats) || materialStats.drawSpeedBonus <= 0.0f) {
                return 1.0f;
            }

            return materialStats.drawSpeedBonus;
        }

        //How well the limb resists taking a permanent set. This is flexibility alone - density is deliberately NOT a
        //term. Density is what makes a limb store energy, and that same stored energy is what tries to deform it, so
        //including density here would credit a material for the very thing it has to survive. A limb fails by
        //staying bent, and springback alone decides that.
        public static float CalculateBowLimbDurability(MaterialStatDefines materialStats, BowStatDefines bowStats = null) {
            if (!CanMaterialFormLimb(materialStats)) {
                return 0.0f;
            }

            return materialStats.flexibility * (bowStats?.limbDurabilityFactor ?? 1.0f);
        }

        //Draw weight is one number, but it has to move two different things - how hard the arrow hits and how flat
        //it flies - and those want separate tuning. Both derive from CalculateBowDrawWeight so there is still a
        //single source of truth for "how strong is this limb": changing a material's density or flexibility moves
        //both together, and these two constants decide only how much of that reaches each axis.
        //
        //They are named here rather than written inline at the patch site because they are the numbers most likely
        //to be retuned, and a value being tuned wants one obvious home.
        public const float BowDamageScale = 1.0f;
        public const float BowVelocityScale = 0.5f;

        //How much the limb multiplies the shot's damage. This scales the COMBINED bow-plus-arrow damage vanilla
        //assembles, not the bow's share alone: a stiffer limb drives whatever arrow is nocked harder, and keeping it
        //on the total is what lets a future arrow axis multiply into the same number instead of competing with it.
        public static float CalculateBowDamageFactor(MaterialStatDefines materialStats, BowStatDefines bowStats = null) {
            if (!CanMaterialFormLimb(materialStats)) {
                return 1.0f;
            }

            return 1.0f + (CalculateBowDrawWeight(materialStats, bowStats) - 1.0f) * BowDamageScale;
        }

        //How much the limb multiplies the arrow's launch velocity. Deliberately damped against the damage factor:
        //draw weight runs to 3.87 at the steel end, and putting that on velocity unscaled fires an arrow flat enough
        //to read as hitscan, which loses the arc that makes bow range a skill. Half the deviation keeps a strong bow
        //visibly faster and still leaves a drop to lead.
        public static float CalculateBowVelocityFactor(MaterialStatDefines materialStats, BowStatDefines bowStats = null) {
            if (!CanMaterialFormLimb(materialStats)) {
                return 1.0f;
            }

            return 1.0f + (CalculateBowDrawWeight(materialStats, bowStats) - 1.0f) * BowVelocityScale;
        }

        //The ceiling on the refund roll. A limb that shrugged off every shot would never wear out at all, which is a
        //bow that never has to be replaced rather than a good bow - the cap is what keeps the top of the table a
        //meaningful choice instead of an ending to the system.
        public const float BowLimbRefundCap = 0.60f;

        //The flexibility span the refund is mapped across. Deliberately WIDER than the range the shipped materials
        //occupy (0.30 to 1.65), so neither end of the real table lands on an anchor. Mapping the best wood exactly
        //onto the cap would put purpleheart at the ceiling untreated, which makes a treatment worthless on the one
        //limb a player is most likely to treat; leaving headroom keeps the cap something to work toward rather than
        //somewhere the table already sits. A material outside this span clamps rather than reading as an error.
        public const float BowLimbRefundMinFlexibility = 0.20f;
        public const float BowLimbRefundMaxFlexibility = 2.00f;

        //The chance a shot's wear is handed straight back, rather than a change to how much total wear the limb has.
        //This is the per-shot half of limb life; the other half scales max durability in the limb behavior itself.
        //Both exist because they answer different questions - how many shots the bow has in it, and how fast it eats
        //them - and a treatment should be able to move the second without silently rewriting the first.
        //
        //Flexibility above the oak baseline is what earns a refund: springback is exactly the property that decides
        //whether a cycle to full draw costs the limb anything. The treatment adds to it, and the total is capped.
        public static float CalculateBowLimbRefundChance(MaterialStatDefines materialStats, TreatmentStatDefines treatmentStats) {
            if (!CanMaterialFormLimb(materialStats)) {
                return 0.0f;
            }

            //Scaled across the span of flexibility the materials actually occupy, rather than measured from oak's
            //1.00. Oak is where the scale was anchored, not where springback begins, so subtracting it made a cliff:
            //every material at or below the baseline collapsed to zero, which flattened kapok at 0.30 and
            //baldcypress at 0.80 into the same limb, and left eight of thirteen woods with no wear-save identity.
            //Proportional mapping gives every material a distinct value and keeps the ordering springback implies.
            //Read from the material alone, with no bow tier term. The span below is a span of FLEXIBILITY, and a
            //bow's build quality is not a change to how springy its wood is: folding the tier in would slide a crude
            //bow down the springback scale for a reason that has nothing to do with springback, and would double-count
            //build quality, which already scales total limb life in GetMaxDurability. The tier decides how big the
            //tank is; the material decides how fast it drains.
            var span = BowLimbRefundMaxFlexibility - BowLimbRefundMinFlexibility;
            var position = (CalculateBowLimbDurability(materialStats) - BowLimbRefundMinFlexibility) / span;
            var fromLimb = Math.Clamp(position, 0.0f, 1.0f) * BowLimbRefundCap;

            //The treatment scales what the limb already earns instead of adding a flat amount. A finish slows a limb
            //taking a set; it cannot make a dead material springy, and a flat bonus made oil the only thing
            //separating the entire bottom of the table while barely registering at the top.
            var fromTreatment = treatmentStats?.limbRefundBonus ?? 0.0f;

            return Math.Clamp(fromLimb * (1.0f + fromTreatment), 0.0f, BowLimbRefundCap);
        }

        //Whether a material can be a limb at all. flexibility and density are left unset (-1.0) rather than defaulted
        //precisely so this question has an answer: a material nobody has given a bow value to reads as "no bow data"
        //instead of silently rating as well as oak. Callers check this before offering a bow recipe.
        //
        //Both axes are required because draw weight is their product: a limb missing either one would compute a draw
        //weight of zero or a negative, so the single gate here is what keeps every bow helper below from needing its
        //own pair of checks.
        public static bool CanMaterialFormLimb(MaterialStatDefines materialStats) {
            return materialStats != null && materialStats.flexibility > 0.0f && materialStats.density > 0.0f;
        }

        //Resolves every stat block a handle and its binding contribute. handleStats is passed in rather than looked
        //up, because callers reach it by different routes - a part define here, an already-resolved tag there - and
        //only the four optional pieces need the same defaulting each time.
        public static HandleStatBundle ResolveHandleStats(HandleStatDefines handleStats, ItemStack handle, BindingStatDefines bindingStats) {
            return new HandleStatBundle {
                Handle = handleStats,
                Binding = bindingStats,
                Grip = HaftModSystem.Stats.GripStats.Get(handle.HasHandleGripTag() ? handle.GetHandleGripTag() : HaftConstants.DefaultGripTag),
                Treatment = HaftModSystem.Stats.TreatmentStats.Get(handle.HasHandleTreatmentTag() ? handle.GetHandleTreatmentTag() : HaftConstants.DefaultTreatmentTag),
                Material = handle.GetHandleMaterialStats()
            };
        }

        //The durability and bonus figures a tool inherits, all four from one bundle so no caller can pass a
        //mismatched set of stat blocks to the individual calculations.
        public static float CalculateHandleDurability(HandleStatBundle stats) {
            return CalculateHandleDurability(stats.Handle, stats.Treatment, stats.Binding, stats.Material);
        }

        public static float CalculateBindingDurability(HandleStatBundle stats) {
            return CalculateBindingDurability(stats.Handle, stats.Binding, stats.Material);
        }

        public static float CalculateSpeedBonus(HandleStatBundle stats) {
            return CalculateSpeedBonus(stats.Handle, stats.Grip, stats.Material);
        }

        public static float CalculateGripChanceToDamage(HandleStatBundle stats) {
            return CalculateGripChanceToDamage(stats.Grip, stats.Treatment);
        }

        //Stores every define of one kind into its dictionary, rejecting entries that cannot be used and reporting a
        //duplicate id unless the config has edits enabled. One routine rather than one per type, so a define that is
        //silently dropped is dropped for the same stated reason whichever kind it is.
        //
        //requiredField names the one field beyond the id that an entry is useless without - a part's stat tag - and
        //returns null when it is missing. A kind with no such field passes null. fullCheck fills in unset numeric
        //fields, and runs only when the config asks for it, since it is a dev-time verification rather than a load
        //step.
        public static void VerifyAndStoreDefines<T>(List<T> list, bool runFullCheck, ref Dictionary<string, T> targetDict, string kindName, System.Func<T, string> requiredField = null, string requiredFieldName = null, Action<T> fullCheck = null) where T : IHaftDefine {
            foreach (var entry in list) {
                if (entry.Id == null) {
                    HaftModSystem.Logger.Error("Attempted to read a " + kindName + " that lacks an id assigned to it. Safely skipping this entry. Likely another mod with a compatability patch is causing this error.");
                    continue;
                }

                if (requiredField != null && requiredField(entry) == null) {
                    HaftModSystem.Logger.Error("Attempted to read a " + kindName + " for id \"" + entry.Id + "\" that lacks a " + requiredFieldName + ". Safely skipping this entry.");
                    continue;
                }

                if (runFullCheck) { //Dev-environment verification, enabled from the base config for addons and compat.
                    fullCheck?.Invoke(entry);
                }

                if (!targetDict.ContainsKey(entry.Id)) {
                    targetDict[entry.Id] = entry;
                } else if (!HaftModSystem.Stats.EnableEdits) {
                    HaftModSystem.Logger.Error("Attempted to add a " + kindName + " that already exists in the Dictionary. There is a second entry for the code " + entry.Id + " being read from the mod files or compat from other mods.");
                }
            }
        }

        //An unset numeric field defaults rather than dropping the entry: the stats come out wrong, but the part still
        //works, and saying so is more useful than refusing to load it.
        private static void DefaultIfUnset(ref float field, float fallback, string fieldName, string kindName, string id) {
            if (field == -1.0f) {
                HaftModSystem.Logger.Error(fieldName + " for " + kindName + " id \"" + id + "\" has not been properly set. Defaulting to " + fallback + " and continuing, stats will be improper but still function.");
                field = fallback;
            }
        }

        private static void WarnIfUnset(string field, string unsetValue, string fieldName, string kindName, string id, string consequence) {
            if (field == unsetValue) {
                HaftModSystem.Logger.Warning(kindName + " with id \"" + id + "\" appears to not have a " + fieldName + " set. " + consequence);
            }
        }

        public static void VerifyAndStoreDefinesInDict(List<HandlePartDefines> list, bool runFullCheck, ref Dictionary<string, HandlePartDefines> targetDict) {
            VerifyAndStoreDefines(list, runFullCheck, ref targetDict, "HandlePartDefine", e => e.handleStatTag, "stat tag");
        }

        public static void VerifyAndStoreDefinesInDict(List<GripPartDefines> list, bool runFullCheck, ref Dictionary<string, GripPartDefines> targetDict) {
            VerifyAndStoreDefines(list, runFullCheck, ref targetDict, "GripPartDefine", e => e.gripStatTag, "stat tag");
        }

        public static void VerifyAndStoreDefinesInDict(List<TreatmentPartDefines> list, bool runFullCheck, ref Dictionary<string, TreatmentPartDefines> targetDict) {
            VerifyAndStoreDefines(list, runFullCheck, ref targetDict, "TreatmentPartDefine", e => e.treatmentStatTag, "stat tag");
        }

        public static void VerifyAndStoreDefinesInDict(List<BindingPartDefines> list, bool runFullCheck, ref Dictionary<string, BindingPartDefines> targetDict) {
            VerifyAndStoreDefines(list, runFullCheck, ref targetDict, "BindingPartDefine", e => e.bindingStatTag, "stat tag");
        }

        public static void VerifyAndStoreDefinesInDict(List<HandleStatDefines> list, bool runFullCheck, ref Dictionary<string, HandleStatDefines> targetDict) {
            VerifyAndStoreDefines(list, runFullCheck, ref targetDict, "HandleStatDefine", fullCheck: e => {
                DefaultIfUnset(ref e.baseHPfactor, 1.0f, "BaseHPFactor", "HandleStatDefine", e.id);
                DefaultIfUnset(ref e.selfHPBonus, 0.0f, "SelfHPBonus", "HandleStatDefine", e.id);
                DefaultIfUnset(ref e.bindingHPBonus, 0.0f, "BindingHPBonus", "HandleStatDefine", e.id);
                DefaultIfUnset(ref e.speedBonus, 0.0f, "SpeedBonus", "HandleStatDefine", e.id);
            });
        }

        public static void VerifyAndStoreDefinesInDict(List<BowStatDefines> list, bool runFullCheck, ref Dictionary<string, BowStatDefines> targetDict) {
            VerifyAndStoreDefines(list, runFullCheck, ref targetDict, "BowStatDefine", fullCheck: e => {
                //Both default to 1.0, the stave tier's own values, so a bow tier a compat mod adds without them is a
                //stave-grade bow rather than a bow that stores nothing and breaks immediately. Unlike flexibility on
                //a material, there is no honest "no data" reading for these: every bow has a build quality, and the
                //question is only whether this config bothered to say what it was.
                DefaultIfUnset(ref e.drawWeightFactor, 1.0f, "DrawWeightFactor", "BowStatDefine", e.id);
                DefaultIfUnset(ref e.limbDurabilityFactor, 1.0f, "LimbDurabilityFactor", "BowStatDefine", e.id);
            });
        }

        public static void VerifyAndStoreDefinesInDict(List<MaterialStatDefines> list, bool runFullCheck, ref Dictionary<string, MaterialStatDefines> targetDict) {
            VerifyAndStoreDefines(list, runFullCheck, ref targetDict, "MaterialStatDefine", fullCheck: e => {
                DefaultIfUnset(ref e.hardness, 1.0f, "Hardness", "MaterialStatDefine", e.id);
                DefaultIfUnset(ref e.nailBindingBonus, 0.0f, "NailBindingBonus", "MaterialStatDefine", e.id);
                //density, flexibility and drawSpeedBonus are deliberately NOT defaulted. Left at -1.0 they mean "no
                //bow data", which is the correct reading for a material nobody has given a bow value to. Defaulting
                //them would instead silently make every such material as good a bow limb as oak, and as quick to draw.
            });
        }

        public static void VerifyAndStoreDefinesInDict(List<GripStatDefines> list, bool runFullCheck, ref Dictionary<string, GripStatDefines> targetDict) {
            VerifyAndStoreDefines(list, runFullCheck, ref targetDict, "GripStatDefine", fullCheck: e => {
                if (e.id != "plain") {
                    WarnIfUnset(e.texturePath, "plain", "texturePath", "GripStatDefine", e.id, "It will lack a texture - is this intentional?");
                    WarnIfUnset(e.langTag, "", "langTag", "GripStatDefine", e.id, "It will likely not localize properly.");
                }
                DefaultIfUnset(ref e.speedBonus, 0.0f, "SpeedBonus", "GripStatDefine", e.id);
                DefaultIfUnset(ref e.chanceToDamage, 1.0f, "ChanceToDamage", "GripStatDefine", e.id);
            });
        }

        public static void VerifyAndStoreDefinesInDict(List<TreatmentStatDefines> list, bool runFullCheck, ref Dictionary<string, TreatmentStatDefines> targetDict) {
            VerifyAndStoreDefines(list, runFullCheck, ref targetDict, "TreatmentStatDefine", fullCheck: e => {
                if (e.id != "plain") {
                    WarnIfUnset(e.langTag, "", "langTag", "TreatmentStatDefine", e.id, "It will likely not localize properly.");
                }
                DefaultIfUnset(ref e.handleHPbonus, 0.0f, "HandleHPBonus", "TreatmentStatDefine", e.id);
            });
        }

        public static void VerifyAndStoreDefinesInDict(List<BindingStatDefines> list, bool runFullCheck, ref Dictionary<string, BindingStatDefines> targetDict) {
            //A metal binding with no metalType cannot say what bits to return when it breaks, so it is dropped rather
            //than stored - hence its own filter rather than a fullCheck, which only ever defaults a value.
            var usable = new List<BindingStatDefines>();
            foreach (var entry in list) {
                if (entry.id != null && entry.isMetal && entry.metalType == null) {
                    HaftModSystem.Logger.Error("The BindingStatDefine with id \"" + entry.id + "\" is a metal binding, but does not have a metalType set. Safely skipping this entry. Likely another mod with a compatability patch is causing this error.");
                    continue;
                }
                usable.Add(entry);
            }

            VerifyAndStoreDefines(usable, runFullCheck, ref targetDict, "BindingStatDefine", fullCheck: e => {
                if (e.id != "none") {
                    WarnIfUnset(e.texturePath, "plain", "texturePath", "BindingStatDefine", e.id, "It will lack a texture - is this intentional?");
                }
                WarnIfUnset(e.langTag, "", "langTag", "BindingStatDefine", e.id, "It will likely not localize properly.");
                DefaultIfUnset(ref e.baseHPfactor, 1.0f, "BaseHPFactor", "BindingStatDefine", e.id);
                DefaultIfUnset(ref e.selfHPBonus, 0.0f, "SelfHPBonus", "BindingStatDefine", e.id);
                DefaultIfUnset(ref e.handleHPBonus, 0.0f, "HandleHPBonus", "BindingStatDefine", e.id);
                DefaultIfUnset(ref e.recoveryPercent, 1.0f, "RecoveryPercent", "BindingStatDefine", e.id);
            });
        }
    }
}
