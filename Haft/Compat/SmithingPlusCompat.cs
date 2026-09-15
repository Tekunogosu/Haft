using System.Linq;
using Vintagestory.API.Common;
using Haft.Utils;

namespace Haft.Compat {

    //Everything Haft knows about Smithing Plus. That mod repairs metal tools by smithing, and its repair path
    //reassigns attributes onto the stack it produces - so Haft has to tell it which of Haft's own attributes to
    //forget, or a repaired tinkered tool comes back carrying stale part data.
    //
    //Two mods answer to this: Smithing Plus itself and Smithing Plus Plus, a maintained fork. The fork ships the same
    //assembly, namespace and Core type and differs only in its modid, so every call here binds to SmithingPlus.Core
    //and resolves against whichever one is loaded. The modid is the only thing that has to be asked about twice,
    //which is what IsLoaded is for. They are mutually exclusive - a user runs one or the other, never both.
    //
    //Unlike the other compat classes here, the mod check lives in IsLoaded rather than at each call site, because
    //there are two ids to test and duplicating that pair at every caller is how one of them gets missed.
    public static class SmithingPlusCompat {

        public const string ModId = "smithingplus";

        //The fork. Newer than upstream and the one Haft compiles against.
        public const string ForkModId = "smithingplusplus";

        /// <summary>
        /// Whether either Smithing Plus or the Smithing Plus Plus fork is loaded. Callers use this in place of a bare
        /// IsModEnabled, so that a check written against one id does not silently skip the other.
        /// </summary>
        public static bool IsLoaded(IModLoader modLoader) {
            return modLoader != null && (modLoader.IsModEnabled(ModId) || modLoader.IsModEnabled(ForkModId));
        }

        /// <summary>
        /// Adds Haft's attributes to Smithing Plus's forgettable-attributes config, so a tool repaired by that mod
        /// drops Haft's part data instead of carrying a stale copy onto the repaired stack.
        /// </summary>
        public static void AddHaftAttributesToForgettableConfig(ICoreAPI api, ILogger logger) {
            //Binds to the type, not the modid: both the mod and its fork ship this exact class, so this one call
            //covers either of them.
            SmithingPlus.Core core = api.ModLoader.GetModSystem<SmithingPlus.Core>();
            if (core == null) {
                logger.Error("Found Smithing Plus loaded, but could not retrieve the Core ModLoader for it! Auto compatability will not work.");
                return;
            }

            if (SmithingPlus.Core.Config.GetToolRepairForgettableAttributes.Contains<string>("tinkeredToolHead")) {
                logger.VerboseDebug("Found possible presence of existing configs already in Smithing Plus for Haft, forgoing the addition! If you have issues, please reset the ToolRepairForgettableAttributes line in the Smithing Plus config.");
                return;
            }

            SmithingPlus.Core.Config.ToolRepairForgettableAttributes = SmithingPlus.Core.Config.ToolRepairForgettableAttributes + HaftAttributes.HaftForgettableAttributes;
            logger.VerboseDebug("Added Haft Attributes to Smithing Plus's Forgettable Attributes config!");
        }
    }
}
