using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Toolsmith.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Toolsmith.Utils {
    public static class ConfigUtility {

        public static string ConfigFilename = "Toolsmith.json";
        public static string ClientConfigFilename = "ToolsmithClient.json";
        public static string StatsFilename = "ToolsmithPartsStats.json";

        public const string AnyWildcardStringPrefix = "@.*("; //For the configs that don't want just the first part of the code, matching the Heads or the Blacklist mainly.
        public const string AnyFirstCodeStartStringPrefix = "@.*:("; //The start of the configs that look for the first part of the code for comparison purposes. Mostly looking for the actual tool/weapon codes!
        public const string ConfigStringPostfix = ").*"; //All config strings end with this! Anything can come after the entered strings.
        public const string ConfigEntrySeparator = "|"; //All entries are separated by this between the Prefix and Postfix!

        public static List<string> SplitToolHeadsConfig;
        public static List<string> SplitTinkerableToolsConfig;
        public static List<string> SplitSmithedToolsConfig;
        public static List<string> SplitBluntToolsConfig;
        public static List<string> SplitBlacklistConfig;

        //The pattern checks below take a CollectibleObject's Code.ToString() and answer whether it is in the
        //respective config. They are used to assign behaviors at load time; at runtime, ask for the behavior itself.
        //Each asks the same question of a different config string: does this code match the wildcard that config
        //was built into.
        private static bool MatchesConfigPattern(string pattern, string code) {
            return code != null && WildcardUtil.Match(pattern, code);
        }

        public static bool IsToolHead(string toolHead) {
            return MatchesConfigPattern(ToolsmithModSystem.Config.ToolHeads, toolHead);
        }

        //Does the target satisfy everything the applied part demands? True when the applied part demands nothing,
        //which is the common case and the reason adding this breaks nothing that already works.
        public static bool TagsSatisfy(string[] requiredTags, ICollection<string> providedTags) {
            if (requiredTags == null || requiredTags.Length == 0) {
                return true;
            }

            foreach (var required in requiredTags) {
                if (string.IsNullOrEmpty(required)) {
                    continue;
                }

                var found = false;
                if (providedTags != null) {
                    foreach (var provided in providedTags) {
                        if (string.Equals(provided, required, StringComparison.OrdinalIgnoreCase)) {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found) {
                    return false;
                }
            }

            return true;
        }

        public static bool IsTinkerableTool(string tool) {
            return MatchesConfigPattern(ToolsmithModSystem.Config.TinkerableTools, tool);
        }

        public static bool IsSinglePartTool(string tool) {
            return MatchesConfigPattern(ToolsmithModSystem.Config.SinglePartTools, tool);
        }

        public static bool IsBluntTool(string tool) {
            return MatchesConfigPattern(ToolsmithModSystem.Config.BluntHeadedTools, tool);
        }

        public static bool IsOnBlacklist(string tool) {
            return MatchesConfigPattern(ToolsmithModSystem.Config.PartBlacklist, tool);
        }

        public static bool IsToolWithWoodInBindingShapes(string tool) {
            return MatchesConfigPattern(ToolsmithModSystem.Config.ToolsWithWoodInBindingShape, tool);
        }

        //A part is registered when the configs hold an entry for its code and that entry has not been switched off.
        //One routine for all four kinds, so a part disabled in a config is disabled the same way whichever kind it is.
        private static bool IsEnabledPart<T>(string code, Dictionary<string, T> dict) where T : ToolsmithPart {
            if (code == null) {
                return false;
            }

            return dict.TryGetValue(code)?.enabled == true;
        }

        public static bool IsToolHandle(string toolHandle, Dictionary<string, HandlePartDefines> dict) {
            return IsEnabledPart(toolHandle, dict);
        }

        public static bool IsToolBinding(string toolBinding, Dictionary<string, BindingPartDefines> dict) {
            return IsEnabledPart(toolBinding, dict);
        }

        public static bool IsValidGripMaterial(string gripMat, Dictionary<string, GripPartDefines> dict) {
            return IsEnabledPart(gripMat, dict);
        }

        public static bool IsValidTreatmentMaterial(string treatmentMat, Dictionary<string, TreatmentPartDefines> dict) {
            return IsEnabledPart(treatmentMat, dict);
        }

        //Takes a built wildcard string back apart into the entries it was made from, so mods can add to the list
        //before it is reassembled. The prefix and postfix are stripped off the first and last entry respectively;
        //everything between is already a bare entry.
        private static List<string> SplitConfigString(string configString) {
            var entries = configString.Split(ConfigEntrySeparator);
            entries[0] = entries[0].Split('(').Last();
            entries[entries.Length - 1] = entries[entries.Length - 1].Split(')').First();
            return entries.ToList();
        }

        private static string JoinConfigString(List<string> entries, string prefix) {
            return prefix + string.Join(ConfigEntrySeparator, entries) + ConfigStringPostfix;
        }

        public static void PrepareAndSplitConfigStrings() {
            SplitToolHeadsConfig = SplitConfigString(ToolsmithModSystem.Config.ToolHeads);
            SplitTinkerableToolsConfig = SplitConfigString(ToolsmithModSystem.Config.TinkerableTools);
            SplitSmithedToolsConfig = SplitConfigString(ToolsmithModSystem.Config.SinglePartTools);
            SplitBluntToolsConfig = SplitConfigString(ToolsmithModSystem.Config.BluntHeadedTools);
            SplitBlacklistConfig = SplitConfigString(ToolsmithModSystem.Config.PartBlacklist);
        }

        public static void MergeAndSetConfigStrings() {
            var completeHeads = JoinConfigString(SplitToolHeadsConfig, AnyWildcardStringPrefix);
            var completeTinkerTools = JoinConfigString(SplitTinkerableToolsConfig, AnyFirstCodeStartStringPrefix);
            var completeSmithedTools = JoinConfigString(SplitSmithedToolsConfig, AnyFirstCodeStartStringPrefix);
            var completeBluntTools = JoinConfigString(SplitBluntToolsConfig, AnyFirstCodeStartStringPrefix);
            var completeBlacklist = JoinConfigString(SplitBlacklistConfig, AnyWildcardStringPrefix);

            if (ToolsmithModSystem.Config.DebugMessages) {
                ToolsmithModSystem.Logger.Warning("The complete config strings before setting them are: ");
                ToolsmithModSystem.Logger.Warning("Heads: " + completeHeads);
                ToolsmithModSystem.Logger.Warning("Tinker Tools: " + completeTinkerTools);
                ToolsmithModSystem.Logger.Warning("Smithed Tools: " + completeSmithedTools);
                ToolsmithModSystem.Logger.Warning("Blunt Tools: " + completeBluntTools);
                ToolsmithModSystem.Logger.Warning("Blacklist: " + completeBlacklist);
            }

            ToolsmithModSystem.Config.ToolHeads = completeHeads;
            ToolsmithModSystem.Config.TinkerableTools = completeTinkerTools;
            ToolsmithModSystem.Config.SinglePartTools = completeSmithedTools;
            ToolsmithModSystem.Config.BluntHeadedTools = completeBluntTools;
            ToolsmithModSystem.Config.PartBlacklist = completeBlacklist;
        }

        public static void AddEntryToToolHeadsConfig(string entry) {
            SplitToolHeadsConfig.Add(entry);
        }

        public static void AddEntryToTinkeredToolsConfig(string entry) {
            SplitTinkerableToolsConfig.Add(entry);
        }

        public static void AddEntryToSmithedToolsConfig(string entry) {
            SplitSmithedToolsConfig.Add(entry);
        }

        public static void AddEntryToBluntToolsConfig(string entry) {
            SplitBluntToolsConfig.Add(entry);
        }

        public static void AddEntryToBlacklistConfig(string entry) {
            SplitBlacklistConfig.Add(entry);
        }

        //Copies a mod config file aside before something is about to overwrite it. A version bump or a
        //disabled-edits flag resets the config back to the shipped defaults, which silently discards every
        //hand-made change in it - so take a copy first and say so in the log, otherwise the only sign that
        //edits are gone is a player noticing a tool stopped working days later.
        //A failed backup must never stop the mod from loading: the config reset still happens, it just
        //happens unprotected, and that is strictly better than refusing to start.
        public static void BackupConfigFile(ICoreAPI api, string filename, string reason) {
            try {
                string configPath = Path.Combine(api.GetOrCreateDataPath("ModConfig"), filename);
                if (!File.Exists(configPath)) { //Nothing written yet - a first run has no edits to lose.
                    return;
                }

                string backupPath = configPath + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                if (File.Exists(backupPath)) { //Two resets inside the same second, so keep both rather than clobbering the first.
                    backupPath = configPath + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
                }

                File.Copy(configPath, backupPath);
                ToolsmithModSystem.Logger.Notification("Backed up " + filename + " to " + Path.GetFileName(backupPath) + " before resetting it to defaults (" + reason + "). Any hand-made edits to the config are in that file.");
            } catch (Exception e) {
                ToolsmithModSystem.Logger.Warning("Could not back up " + filename + " before resetting it to defaults (" + reason + "). The reset will still happen, so any edits in it are about to be lost. Reason: " + e.Message);
            }
        }
    }
}
