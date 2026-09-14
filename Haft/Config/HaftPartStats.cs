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
            var handleDur = HaftConstants.PartDurabilityBase * handleStats.baseHPfactor * woodStats.densityFactor;
            handleDur += handleDur * handleStats.selfHPBonus;
            handleDur += handleDur * treatmentStats.handleHPbonus;
            handleDur += handleDur * bindingStats.handleHPBonus;
            return handleDur;
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

        public static void VerifyAndStoreDefinesInDict(List<MaterialStatDefines> list, bool runFullCheck, ref Dictionary<string, MaterialStatDefines> targetDict) {
            foreach (var entry in list) {
                //An older config, and every compat mod written against one, spells this hardnessFactor. Fold it in
                //before the check below, so such an entry reads as complete rather than being reported unset.
                if (entry.densityFactor == -1.0f && entry.hardnessFactor != -1.0f) {
                    entry.densityFactor = entry.hardnessFactor;
                }
            }

            VerifyAndStoreDefines(list, runFullCheck, ref targetDict, "MaterialStatDefine", fullCheck: e => {
                DefaultIfUnset(ref e.densityFactor, 1.0f, "DensityFactor", "MaterialStatDefine", e.id);
                DefaultIfUnset(ref e.nailBindingBonus, 0.0f, "NailBindingBonus", "MaterialStatDefine", e.id);
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
