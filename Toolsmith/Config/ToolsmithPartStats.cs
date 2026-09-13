using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Toolsmith.Utils;
using Vintagestory.GameContent;

namespace Toolsmith.Config {

    public class ToolsmithPartStats {

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

    public static class ToolsmithPartStatsHelpers {

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
            var handleDur = ToolsmithConstants.PartDurabilityBase * handleStats.baseHPfactor * woodStats.densityFactor;
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
            var bindingDur = ToolsmithConstants.PartDurabilityBase * bindingStats.baseHPfactor;
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

        public static void VerifyAndStoreDefinesInDict(List<HandlePartDefines> list, bool runFullCheck, ref Dictionary<string, HandlePartDefines> targetDict) {
            foreach (var entry in list) {
                if (entry.id == null) {
                    ToolsmithModSystem.Logger.Error("Attempted to read a HandlePartDefine that lacks an id assigned to it. Safely skipping this entry. Likely another mod with a compatability patch is causing this error.");
                    continue;
                }

                if (entry.handleStatTag == null) {
                    ToolsmithModSystem.Logger.Error("Attempted to read a HandlePartDefine for id \"" + entry.id + "\"that lacks a stat tag. Safely skipping this entry");
                    continue;
                }

                if (!targetDict.ContainsKey(entry.id)) {
                    targetDict[entry.id] = entry;
                } else if (!ToolsmithModSystem.Stats.EnableEdits) {
                    ToolsmithModSystem.Logger.Error("Attempted to add a HandlePartDefine that already exists in the Dictionary. There is a second entry for the code " + entry.id + " being read from the mod files or compat from other mods.");
                }
            }
        }

        public static void VerifyAndStoreDefinesInDict(List<GripPartDefines> list, bool runFullCheck, ref Dictionary<string, GripPartDefines> targetDict) {
            foreach (var entry in list) {
                if (entry.id == null) {
                    ToolsmithModSystem.Logger.Error("Attempted to read a GripPartDefine that lacks an id assigned to it. Safely skipping this entry. Likely another mod with a compatability patch is causing this error.");
                    continue;
                }

                if (entry.gripStatTag == null) {
                    ToolsmithModSystem.Logger.Error("Attempted to read a GripPartDefine for id \"" + entry.id + "\"that lacks a stat tag. Safely skipping this entry");
                    continue;
                }

                if (!targetDict.ContainsKey(entry.id)) {
                    targetDict[entry.id] = entry;
                } else if (!ToolsmithModSystem.Stats.EnableEdits) {
                    ToolsmithModSystem.Logger.Error("Attempted to add a GripPartDefine that already exists in the Dictionary. There is a second entry for the code " + entry.id + " being read from the mod files or compat from other mods.");
                }
            }
        }

        public static void VerifyAndStoreDefinesInDict(List<TreatmentPartDefines> list, bool runFullCheck, ref Dictionary<string, TreatmentPartDefines> targetDict) {
            foreach (var entry in list) {
                if (entry.id == null) {
                    ToolsmithModSystem.Logger.Error("Attempted to read a TreatmentPartDefine that lacks an id assigned to it. Safely skipping this entry. Likely another mod with a compatability patch is causing this error.");
                    continue;
                }

                if (entry.treatmentStatTag == null) {
                    ToolsmithModSystem.Logger.Error("Attempted to read a TreatmentPartDefine for id \"" + entry.id + "\"that lacks a stat tag. Safely skipping this entry");
                    continue;
                }

                if (!targetDict.ContainsKey(entry.id)) {
                    targetDict[entry.id] = entry;
                } else if (!ToolsmithModSystem.Stats.EnableEdits) {
                    ToolsmithModSystem.Logger.Error("Attempted to add a TreatmentPartDefine that already exists in the Dictionary. There is a second entry for the code " + entry.id + " being read from the mod files or compat from other mods.");
                }
            }
        }

        public static void VerifyAndStoreDefinesInDict(List<BindingPartDefines> list, bool runFullCheck, ref Dictionary<string, BindingPartDefines> targetDict) {
            foreach (var entry in list) {
                if (entry.id == null) {
                    ToolsmithModSystem.Logger.Error("Attempted to read a BindingPartDefine that lacks an id assigned to it. Safely skipping this entry. Likely another mod with a compatability patch is causing this error.");
                    continue;
                }

                if (entry.bindingStatTag == null) {
                    ToolsmithModSystem.Logger.Error("Attempted to read a BindingPartDefine for id \"" + entry.id + "\"that lacks a stat tag. Safely skipping this entry");
                    continue;
                }

                if (!targetDict.ContainsKey(entry.id)) {
                    targetDict[entry.id] = entry;
                } else if (!ToolsmithModSystem.Stats.EnableEdits) {
                    ToolsmithModSystem.Logger.Error("Attempted to add a BindingPartDefine that already exists in the Dictionary. There is a second entry for the code " + entry.id + " being read from the mod files or compat from other mods.");
                }
            }
        }

        public static void VerifyAndStoreDefinesInDict(List<HandleStatDefines> list, bool runFullCheck, ref Dictionary<string, HandleStatDefines> targetDict) {
            foreach (var entry in list) {
                if (entry.id == null) {
                    ToolsmithModSystem.Logger.Error("Attempted to read a HandleStatDefine that lacks an id assigned to it. Safely skipping this entry. Likely another mod with a compatability patch is causing this error.");
                    continue;
                }

                if (runFullCheck) { //Things in here don't need to run all the time unless in a dev environment. Can be enabled in the base config for easy verification for addons or compat.
                    if (entry.baseHPfactor == -1.0f) {
                        ToolsmithModSystem.Logger.Error("BaseHPFactor for HandleStatDefine id \"" + entry.id + "\" has not been properly set. Defaulting to 1.0 and continuing, stats will be improper but still function.");
                        entry.baseHPfactor = 1.0f;
                    }

                    if (entry.selfHPBonus == -1.0f) {
                        ToolsmithModSystem.Logger.Error("SelfHPBonus for HandleStatDefine id \"" + entry.id + "\" has not been properly set. Defaulting to 0.0 and continuing, stats will be improper but still function.");
                        entry.selfHPBonus = 0.0f;
                    }

                    if (entry.bindingHPBonus == -1.0f) {
                        ToolsmithModSystem.Logger.Error("BindingHPBonus for HandleStatDefine id \"" + entry.id + "\" has not been properly set. Defaulting to 0.0 and continuing, stats will be improper but still function.");
                        entry.bindingHPBonus = 0.0f;
                    }

                    if (entry.speedBonus == -1.0f) {
                        ToolsmithModSystem.Logger.Error("SpeedBonus for HandleStatDefine id \"" + entry.id + "\" has not been properly set. Defaulting to 0.0 and continuing, stats will be improper but still function.");
                        entry.speedBonus = 0.0f;
                    }
                }

                if (!targetDict.ContainsKey(entry.id)) {
                    targetDict[entry.id] = entry;
                } else if (!ToolsmithModSystem.Stats.EnableEdits) {
                    ToolsmithModSystem.Logger.Error("Attempted to add a HandleStatDefine that already exists in the Dictionary. There is a second entry for the code " + entry.id + " being read from the mod files or compat from other mods.");
                }
            }
        }

        public static void VerifyAndStoreDefinesInDict(List<MaterialStatDefines> list, bool runFullCheck, ref Dictionary<string, MaterialStatDefines> targetDict) {
            foreach (var entry in list) {
                if (entry.id == null) {
                    ToolsmithModSystem.Logger.Error("Attempted to read a MaterialStatDefine that lacks an id assigned to it. Safely skipping this entry. Likely another mod with a compatability patch is causing this error.");
                    continue;
                }

                //An older config, and every compat mod written against one, spells this hardnessFactor. Fold it in
                //before the check below, so such an entry reads as complete rather than being reported unset.
                if (entry.densityFactor == -1.0f && entry.hardnessFactor != -1.0f) {
                    entry.densityFactor = entry.hardnessFactor;
                }

                if (runFullCheck) {
                    if (entry.densityFactor == -1.0f) {
                        ToolsmithModSystem.Logger.Error("DensityFactor for MaterialStatDefine with id \"" + entry.id + "\" has not been properly set. Defaulting to 1.0 and continuing, stats will be improper but still function.");
                        entry.densityFactor = 1.0f;
                    }

                    if (entry.nailBindingBonus == -1.0f) {
                        ToolsmithModSystem.Logger.Error("NailBindingBonus for MaterialStatDefine with id \"" + entry.id + "\" has not been properly set. Defaulting to 0.0 and continuing, stats will be improper but still function.");
                        entry.nailBindingBonus = 0.0f;
                    }
                }

                if (!targetDict.ContainsKey(entry.id)) {
                    targetDict[entry.id] = entry;
                } else if (!ToolsmithModSystem.Stats.EnableEdits) {
                    ToolsmithModSystem.Logger.Error("Attempted to add a MaterialStatDefine that already exists in the Dictionary. There is a second entry for the code " + entry.id + " being read from the mod files or compat from other mods.");
                }
            }
        }

        public static void VerifyAndStoreDefinesInDict(List<GripStatDefines> list, bool runFullCheck, ref Dictionary<string, GripStatDefines> targetDict) {
            foreach (var entry in list) {
                if (entry.id == null) {
                    ToolsmithModSystem.Logger.Error("Attempted to read a GripStatDefine that lacks an id assigned to it. Safely skipping this entry. Likely another mod with a compatability patch is causing this error.");
                    continue;
                }

                if (runFullCheck) {
                    if (entry.id != "plain" && entry.texturePath == "plain") {
                        ToolsmithModSystem.Logger.Warning("GripStatDefine with id \"" + entry.id + "\" appears to not have a texturePath set. It will lack a texture - is this intentional?");
                    }

                    if (entry.id != "plain" && entry.langTag == "") {
                        ToolsmithModSystem.Logger.Warning("GripStatDefine with id \"" + entry.id + "\" appears to not have a langTag set. It will likely not localize properly.");
                    }

                    if (entry.speedBonus == -1.0f) {
                        ToolsmithModSystem.Logger.Error("SpeedBonus for GripStatDefine with id \"" + entry.id + "\" has not been properly set. Defaulting to 0.0 and continuing, stats will be improper but still function.");
                        entry.speedBonus = 0.0f;
                    }

                    if (entry.chanceToDamage == -1.0f) {
                        ToolsmithModSystem.Logger.Error("ChanceToDamage for GripStatDefine with id \"" + entry.id + "\" has not been properly set. Defaulting to 1.0 and continuing, stats will be improper but still function.");
                        entry.chanceToDamage = 1.0f;
                    }
                }

                if (!targetDict.ContainsKey(entry.id)) {
                    targetDict[entry.id] = entry;
                } else if (!ToolsmithModSystem.Stats.EnableEdits) {
                    ToolsmithModSystem.Logger.Error("Attempted to add a GripStatDefine that already exists in the Dictionary. There is a second entry for the code " + entry.id + " being read from the mod files or compat from other mods.");
                }
            }
        }

        public static void VerifyAndStoreDefinesInDict(List<TreatmentStatDefines> list, bool runFullCheck, ref Dictionary<string, TreatmentStatDefines> targetDict) {
            foreach (var entry in list) {
                if (entry.id == null) {
                    ToolsmithModSystem.Logger.Error("Attempted to read a TreatmentStatDefine that lacks an id assigned to it. Safely skipping this entry. Likely another mod with a compatability patch is causing this error.");
                    continue;
                }

                if (runFullCheck) {
                    if (entry.id != "plain" && entry.langTag == "") {
                        ToolsmithModSystem.Logger.Warning("TreatmentStatDefine with id \"" + entry.id + "\" appears to not have a langTag set. It will likely not localize properly.");
                    }

                    if (entry.handleHPbonus == -1.0f) {
                        ToolsmithModSystem.Logger.Error("HandleHPBonus for TreatmentStatDefine with id \"" + entry.id + "\" has not been properly set. Defaulting to 0.0 and continuing, stats will be improper but still function.");
                        entry.handleHPbonus = 0.0f;
                    }
                }

                if (!targetDict.ContainsKey(entry.id)) {
                    targetDict[entry.id] = entry;
                } else if (!ToolsmithModSystem.Stats.EnableEdits) {
                    ToolsmithModSystem.Logger.Error("Attempted to add a TreatmentStatDefine that already exists in the Dictionary. There is a second entry for the code " + entry.id + " being read from the mod files or compat from other mods.");
                }
            }
        }

        public static void VerifyAndStoreDefinesInDict(List<BindingStatDefines> list, bool runFullCheck, ref Dictionary<string, BindingStatDefines> targetDict) {
            foreach (var entry in list) {
                if (entry.id == null) {
                    ToolsmithModSystem.Logger.Error("Attempted to read a BindingStatDefine that lacks an id assigned to it. Safely skipping this entry. Likely another mod with a compatability patch is causing this error.");
                    continue;
                }

                if (entry.isMetal && entry.metalType == null) {
                    ToolsmithModSystem.Logger.Error("The BindingStatDefine with id \"" + entry.id + "\" is a metal binding, but does not have a metalType set. Safely skipping this entry. Likely another mod with a compatability patch is causing this error.");
                    continue;
                }

                if (runFullCheck) {
                    if (entry.id != "none" && entry.texturePath == "plain") {
                        ToolsmithModSystem.Logger.Warning("BindingStatDefine with id \"" + entry.id + "\" appears to not have a texturePath set. It will lack a texture - is this intentional?");
                    }

                    if (entry.langTag == "") {
                        ToolsmithModSystem.Logger.Warning("BindingStatDefine with id \"" + entry.id + "\" appears to not have a langTag set. It will likely not localize properly.");
                    }

                    if (entry.baseHPfactor == -1.0f) {
                        ToolsmithModSystem.Logger.Error("BaseHPFactor for BindingStatDefine with id \"" + entry.id + "\" has not been set. Defaulting to 1.0 and continuing, stats will be improper but still function.");
                        entry.baseHPfactor = 1.0f;
                    }

                    if (entry.selfHPBonus == -1.0f) {
                        ToolsmithModSystem.Logger.Error("SelfHPBonus for BindingStatDefine with id \"" + entry.id + "\" has not been set. Defaulting to 0.0 and continuing, stats will be improper but still function.");
                        entry.selfHPBonus = 0.0f;
                    }

                    if (entry.handleHPBonus == -1.0f) {
                        ToolsmithModSystem.Logger.Error("HandleHPBonus for BindingStatDefine with id \"" + entry.id + "\" has not been set. Defaulting to 0.0 and continuing, stats will be improper but still function.");
                        entry.handleHPBonus = 0.0f;
                    }

                    if (entry.recoveryPercent == -1.0f) {
                        ToolsmithModSystem.Logger.Error("RecoveryPercent for BindingStatDefine with id \"" + entry.id + "\" has not been set. Defaulting to 1.0 and continuing, stats will be improper but still function.");
                        entry.recoveryPercent = 1.0f;
                    }
                }

                if (!targetDict.ContainsKey(entry.id)) {
                    targetDict[entry.id] = entry;
                } else if (!ToolsmithModSystem.Stats.EnableEdits) {
                    ToolsmithModSystem.Logger.Error("Attempted to add a BindingStatDefine that already exists in the Dictionary. There is a second entry for the code " + entry.id + " being read from the mod files or compat from other mods.");
                }
            }
        }

    }

    /*public class HandlePartDefines {
        public string handleStatTag;
        public bool canHaveGrip = false;
        public string handleShapePath = "";
        public bool canBeTreated = false;
        public float dryingTimeMult = 1.0f;
    }*/

    /*public class GripPartDefines {
        public string gripStatTag;
        public string gripShapePath = "";
        public string gripTextureOverride = "";
    }*/

    /*public class TreatmentPartDefines {
        public string treatmentStatTag;
        public int dryingHours; //Base number of hours it takes to dry a handle, multiplied by the handle's drying time multiplier when applied.
        public bool isLiquid = false;
        public float litersUsed = 0.0f;
    }*/

    /*public class BindingPartDefines {
        public string bindingStatTag;
        public string bindingShapePath = "";
        public string bindingTextureOverride = "";
    }*/

    /*public class HandleStatDefines { //In an effort to keep things similarly vanilla for durability values, the baseHPfactor is a multiplier on the base durability of the tool-to-be-crafted
        public string id; //An ID to help access and find what it is - make sure this is the same as the Dictionary Key. It might help to keep an id associated with the stats.
        public float baseHPfactor; //It's the main part of Handles and Bindings.
        public float selfHPBonus; //For more advanced handles, provides an additional multiplier for the handle's health as a bonus ontop
        public float bindingHPBonus; //Advanced handles can provide a small bonus to the Binding's HP
        public float speedBonus; //Advanced handles can make it easier to use the tool as well!
    }*/

    /*public class GripStatDefines {
        public string id;
        public string texturePath = "plain";
        public string langTag = ""; //A tag to set for localization purposes that describes the grip on the tool IE: "grip-cloth" for cloth
        public float speedBonus; //The best speed bonuses come from the grip of the tool. If you can hold it better, you can use it faster...
        public float chanceToDamage; //And more efficiently too. Gives the handle a chance to ignore damage!
    }*/

    /*public class TreatmentStatDefines {
        public string id;
        public string langTag = ""; //A tag to set for localization purposes that describes the treatment on the tool IE: "treatment-wax" for wax
        public float handleHPbonus; //Treating the handle makes it last longer
    }*/

    /*public class BindingStatDefines {
        public string id;
        public string texturePath = "plain";
        public string langTag = "";
        public float baseHPfactor;
        public float selfHPBonus;
        public float handleHPBonus;
        public float recoveryPercent; //If the HP is below this percent, then the binding is ruined if another part breaks
        public bool isMetal; //If true and the bindings break, try and return some bits
        public string metalType; //For ease of returning the bits, the material/metal variant of bits to return!
    }*/
}
