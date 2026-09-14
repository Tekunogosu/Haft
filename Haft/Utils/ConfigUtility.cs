using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Haft.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Haft.Utils {
    public static class ConfigUtility {

        public static string ConfigFilename = "Haft.json";
        public static string ClientConfigFilename = "HaftClient.json";
        public static string StatsFilename = "HaftPartsStats.json";

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
            return MatchesConfigPattern(HaftModSystem.Config.ToolHeads, toolHead);
        }

        //Does the target satisfy everything the applied part demands? True when the applied part demands nothing,
        //which is the common case and the reason adding this breaks nothing that already works.
        //Whether a requirement list names this specific tag as one of its alternatives. Used to tell a follow-on
        //treatment (oil over bluing, which asks for "blued") apart from one that merely happens to be applicable,
        //so only the former is allowed onto an already-treated handle.
        public static bool TagsRequireAnyOf(string[] requiredTags, string tag) {
            if (requiredTags == null || string.IsNullOrEmpty(tag)) {
                return false;
            }

            foreach (var required in requiredTags) {
                if (string.IsNullOrEmpty(required)) {
                    continue;
                }
                foreach (var alternative in required.Split('|')) {
                    if (string.Equals(alternative.Trim(), tag, StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool TagsSatisfy(string[] requiredTags, ICollection<string> providedTags) {
            if (requiredTags == null || requiredTags.Length == 0) {
                return true;
            }

            foreach (var required in requiredTags) {
                if (string.IsNullOrEmpty(required)) {
                    continue;
                }

                //A requirement may list alternatives separated by '|', satisfied if ANY of them is present. Oil
                //needs this: it goes on bare wood or over a blued metal surface, and TreatmentParts is keyed by
                //item code, so one oil entry has to cover both rather than splitting into two.
                var alternatives = required.Split('|');

                var found = false;
                if (providedTags != null) {
                    foreach (var alternative in alternatives) {
                        var wanted = alternative.Trim();
                        if (wanted.Length == 0) {
                            continue;
                        }
                        foreach (var provided in providedTags) {
                            if (string.Equals(provided, wanted, StringComparison.OrdinalIgnoreCase)) {
                                found = true;
                                break;
                            }
                        }
                        if (found) {
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
            return MatchesConfigPattern(HaftModSystem.Config.TinkerableTools, tool);
        }

        public static bool IsSinglePartTool(string tool) {
            return MatchesConfigPattern(HaftModSystem.Config.SinglePartTools, tool);
        }

        public static bool IsBluntTool(string tool) {
            return MatchesConfigPattern(HaftModSystem.Config.BluntHeadedTools, tool);
        }

        public static bool IsOnBlacklist(string tool) {
            return MatchesConfigPattern(HaftModSystem.Config.PartBlacklist, tool);
        }

        public static bool IsToolWithWoodInBindingShapes(string tool) {
            return MatchesConfigPattern(HaftModSystem.Config.ToolsWithWoodInBindingShape, tool);
        }

        //A part is registered when the configs hold an entry for its code and that entry has not been switched off.
        //One routine for all four kinds, so a part disabled in a config is disabled the same way whichever kind it is.
        private static bool IsEnabledPart<T>(string code, Dictionary<string, T> dict) where T : HaftPart {
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
            SplitToolHeadsConfig = SplitConfigString(HaftModSystem.Config.ToolHeads);
            SplitTinkerableToolsConfig = SplitConfigString(HaftModSystem.Config.TinkerableTools);
            SplitSmithedToolsConfig = SplitConfigString(HaftModSystem.Config.SinglePartTools);
            SplitBluntToolsConfig = SplitConfigString(HaftModSystem.Config.BluntHeadedTools);
            SplitBlacklistConfig = SplitConfigString(HaftModSystem.Config.PartBlacklist);
        }

        public static void MergeAndSetConfigStrings() {
            var completeHeads = JoinConfigString(SplitToolHeadsConfig, AnyWildcardStringPrefix);
            var completeTinkerTools = JoinConfigString(SplitTinkerableToolsConfig, AnyFirstCodeStartStringPrefix);
            var completeSmithedTools = JoinConfigString(SplitSmithedToolsConfig, AnyFirstCodeStartStringPrefix);
            var completeBluntTools = JoinConfigString(SplitBluntToolsConfig, AnyFirstCodeStartStringPrefix);
            var completeBlacklist = JoinConfigString(SplitBlacklistConfig, AnyWildcardStringPrefix);

            if (HaftModSystem.Config.DebugMessages) {
                HaftModSystem.Logger.Warning("The complete config strings before setting them are: ");
                HaftModSystem.Logger.Warning("Heads: " + completeHeads);
                HaftModSystem.Logger.Warning("Tinker Tools: " + completeTinkerTools);
                HaftModSystem.Logger.Warning("Smithed Tools: " + completeSmithedTools);
                HaftModSystem.Logger.Warning("Blunt Tools: " + completeBluntTools);
                HaftModSystem.Logger.Warning("Blacklist: " + completeBlacklist);
            }

            HaftModSystem.Config.ToolHeads = completeHeads;
            HaftModSystem.Config.TinkerableTools = completeTinkerTools;
            HaftModSystem.Config.SinglePartTools = completeSmithedTools;
            HaftModSystem.Config.BluntHeadedTools = completeBluntTools;
            HaftModSystem.Config.PartBlacklist = completeBlacklist;
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
        //Whether the config file on disk has a different SET OF KEYS than the type it deserializes into - a key
        //added by a mod upgrade, or one retired by it. Only the names are compared; values are deliberately ignored,
        //because a value the user changed is theirs to keep and a value the mod rebuilds (the regex strings) differs
        //on almost every start. Comparing values instead produced one worthless backup per server start.
        //
        //Returns false on any read or parse failure: a backup is a safety net, and a corrupt or unreadable file is
        //not a reason to refuse to start.
        public static bool ConfigKeysDifferFromDefaults<T>(ICoreAPI api, string filename) where T : new() {
            try {
                string configPath = Path.Combine(api.GetOrCreateDataPath("ModConfig"), filename);
                if (!File.Exists(configPath)) { //A first run has no file, so no keys to have drifted.
                    return false;
                }

                var onDisk = JObject.Parse(File.ReadAllText(configPath));
                var expected = JObject.FromObject(new T());

                var onDiskKeys = onDisk.Properties().Select(prop => prop.Name);
                var expectedKeys = expected.Properties().Select(prop => prop.Name);

                return !onDiskKeys.OrderBy(name => name, StringComparer.Ordinal)
                    .SequenceEqual(expectedKeys.OrderBy(name => name, StringComparer.Ordinal), StringComparer.Ordinal);
            } catch (Exception e) {
                HaftModSystem.Logger.Warning("Could not compare the keys in " + filename + " against the current defaults, so no backup was taken. Reason: " + e.Message);
                return false;
            }
        }

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
                HaftModSystem.Logger.Notification("Backed up " + filename + " to " + Path.GetFileName(backupPath) + " before resetting it to defaults (" + reason + "). Any hand-made edits to the config are in that file.");
            } catch (Exception e) {
                HaftModSystem.Logger.Warning("Could not back up " + filename + " before resetting it to defaults (" + reason + "). The reset will still happen, so any edits in it are about to be lost. Reason: " + e.Message);
            }
        }
    }
}
