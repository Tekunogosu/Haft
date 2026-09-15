using Vintagestory.API.Datastructures;

namespace Haft.Utils {
    public static class HaftConstants {
        public const string SmithWithBitsEnabled = "Haft_SmithWithBits";
        public const string DisabledMultiPartRenders = "Haft_MultiPartRendering";

        public const string FallbackHeadCode = "game:candle";
        public const string DefaultHandleCode = "game:stick";
        public const string BoneHandleCode = "game:bone";
        public const string DefaultGripTag = "plain";
        public const string DefaultTreatmentTag = "none";
        public const string ToolBundleCode = "haft:tinkertoolparts";
        public const string HandleBlankCode = "supportbeam"; //As in, the 'blank' that is crafted into a handle! If that recipe of Knife + this changes, make sure to change this!
        public const string MetalHandleBlankCode = "ingot"; //The blank a metal handle is worked from in the grid. Its metal is read the same way a supportbeam's wood is, off the last code part.
        public const string SandpaperCode = "haft:sandpaper";
        public const string FirewoodCode = "game:firewood";
        public const string WorkItemCode = "game:workitem";
        public const string WorkItemFirstCodePart = "workitem"; //The same thing without its domain, for FirstCodePart comparisons. A smithing recipe hands in workitem-<metal> rather than the ingot it began as, so a metal handle reads its metal off that code exactly as the grid reads it off an ingot.
        public const string IngotCode = "game:ingot";
        public const string HandleWoodTexturePathMinusType = "haft:block/tools/tool";
        public const string HandleMetalTexturePathMinusType = "game:block/metal/plate/"; //The plate textures the metal bindings already use, so most metals have a handle texture without one being drawn for it.
        public const string IngotMetalBackupPathMinusType = "game:block/metal/ingot/"; //Four metals - blistersteel, cupronickel and the two solders - have no plate texture. Their ingot texture stands in, the same way a debarked log stands in for a wood with no tool texture.
        public const string DebarkedWoodBackupPathMinusType = "game:block/wood/debarked/";
        public const string DefaultGripFallbackTexture = "game:block/cloth/reedrope";
        public const string LightTreatementOverlayPath = "haft:block/overlays/lighter";
        public const string DarkTreatementOverlayPath = "haft:block/overlays/darker";
        public const string BluedTreatementOverlayPath = "haft:block/overlays/blued"; //Bluing colours the metal rather than darkening it, so it overrides the light/dark choice the other treatments make.

        public const float TimeToCraftTinkerTool = 2.5f;
        public const float StartingSharpnessMult = 0.85f;
        public const float NonMetalStartingSharpnessMult = 0.66f;
        public const float HighSharpnessSpeedBonusMult = 0.05f;
        public const float LowSharpnessSpeedMalusMult = -0.1f;
        public const float SharpenInterval = 0.4f;
        public const float DoNotSharpenBelowPercent = 0.01f;
        public const int NumBitsReturnMinimum = 2;
        public const float StickAndBoneFailurePercent = 0.25f;
        public const float OtherHandleFailurePercent = 0.05f;
        public const int NumHammerStrikesForWorkbenchCraftAction = 3;

        public const string ModularPartRenderingFromAttributesMeshRefs = "HaftModularPartRenderingMeshRefs";
        public const string WorkbenchItemRenderingMeshRefs = "HaftWorkbenchItemRenderingMeshRefs";
        public const string WorkbenchSlotShapesCache = "workbenchSlotMarkerTextures";
        public const string WorkbenchSlotMarkerShapePath = "haft:shapes/block/workbench-slotmarker";
        public const string WorkbenchSlotMarkerEmptyPath = "haft:block/workbenchslots/empty-slot";
        public const string WorkbenchSlotMarkerHeadPath = "haft:block/workbenchslots/toolhead-slot";
        public const string WorkbenchSlotMarkerHandlePath = "haft:block/workbenchslots/toolhandle-slot";
        public const string WorkbenchSlotMarkerBindingPath = "haft:block/workbenchslots/toolbinding-slot";

        public const string HaftConfigKey = "HaftConfigs";
        public const string HaftStatsKey = "HaftPartStats";
        public const string HaftWoodInToolBindingsData = "HaftWoodInBindingsList";

        public static TagSet HaftHeadTag;
        public static TagSet HaftHandleTag;
        public static TagSet HaftBindingTag;
        public static TagSet HaftMaintenanceItemTag;
        public static TagSet HaftPartTag;

        //The keys for accessing the default part entries themselves, to recieve their stat key blocks
        public const string DefaultHandlePartKey = "stick";
        public const string DefaultBindingPartKey = "none";

        //The keys for accessing the default stat blocks for the different parts
        public const string DefaultBindingStatKey = "none";
        //The durability every handle and binding is built from, before its own tier factor, its material and its
        //bonuses. Flat and shared on purpose: a part's durability comes from what that part is made of, not from the
        //tool head it happens to be attached to.
        public const int PartDurabilityBase = 1000;

        //Charcoal bluing forms the black oxide layer at around 300C - low by forge standards, well under working
        //heat - and the part then air-cools. Nothing is quenched: the layer forms while hot and cooling is incidental,
        //which is why this needs a temperature reached rather than a timed sequence.
        public const float BluingTemperature = 300.0f;
        public const int AdhesiveGripRecipeGroup = 4; //The generated recipe group that backs a grip with an adhesive. Groups 2 and 3 are grips and treatments.
        public const string AdhesiveBindingStatTag = "glue"; //The binding stat block whose members count as adhesives for backing a grip. Vanilla pitch glue plus whatever Butchery and friends add under the same tag.
        public const string AdhesiveBackedTag = "adhesive-backed"; //What a grip provides once it has been backed with an adhesive. A handle too smooth for a grip to grab demands this in its requiresTags.
        public const string BluingTreatmentTag = "blued";
        public const float BluingCooledTemperature = 100.0f; //A handle must fall below this before it counts as cooled and can be blued. Stops a piece straight off the anvil blueing on its way down, which would make the finish something the player received rather than something they did.

        public const string DefaultMaterialStatKey = "oak"; //Handles with no material of their own - stick, bone, crude - and handles saved before the material tag existed fall back to this. Matches the renderer's own default wood texture.
    }

    //The material keys this mod's own code names directly, as constants rather than as literals typed at each call
    //site, so that a misspelling is a compile error instead of a silent fall back to oak at runtime.
    //
    //This is deliberately not an enum, and must not become one. The set of materials is open: a compat mod ships its
    //own MaterialStatDefines as a Json asset - uraniumexpanded adds ferrousuranium and uraniumsteel, meteoricsteel
    //adds its own - and a closed set would leave those mods unable to name what they add. The constants cover the
    //keys Haft itself references; every other key still travels as a plain string, exactly as the part, grip,
    //binding and treatment tags already do.
    //
    //VerifyMaterialsAreLoaded checks each one actually resolves once the configs are read.
    public static class HandleMaterials {
        public const string Oak = "oak";

        public const string Bismuth = "bismuth";
        public const string BismuthBronze = "bismuthbronze";
        public const string BlackBronze = "blackbronze";
        public const string Brass = "brass";
        public const string Copper = "copper";
        public const string CuproNickel = "cupronickel";
        public const string Electrum = "electrum";
        public const string Gold = "gold";
        public const string Iron = "iron";
        public const string Lead = "lead";
        public const string MeteoricIron = "meteoriciron";
        public const string Molybdochalkos = "molybdochalkos";
        public const string Nickel = "nickel";
        public const string Silver = "silver";
        public const string Steel = "steel";
        public const string Tin = "tin";
        public const string TinBronze = "tinbronze";
        public const string Zinc = "zinc";

        //Every constant above, for the startup check. A new constant belongs in both places.
        public static readonly string[] All = {
            Oak, Bismuth, BismuthBronze, BlackBronze,
            Brass, Copper, CuproNickel, Electrum,
            Gold, Iron, Lead, MeteoricIron,
            Molybdochalkos, Nickel, Silver, Steel,
            Tin, TinBronze, Zinc
        };
    }
}
