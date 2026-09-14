
namespace Haft.Utils {
    //Every attribute name this mod writes, as a constant rather than a literal at each call site.
    //
    //These names are saved world data: renaming one silently orphans the value on every existing item, so a name here
    //is fixed once it has shipped. New ones are camel case, matching the rest.
    public static class HaftAttributes {
        public const string ToolHead = "tinkeredToolHead";
        public const string ToolSharpnessCurrent = "toolSharpnessCurrent"; //For both the tool and the head alone
        public const string ToolSharpnessMax = "toolSharpnessMax"; //For both the tool and the head alone
        public const string ToolHandle = "tinkeredToolHandle";
        public const string ToolHandleCurrentDur = "tinkeredToolHandleDurability";
        public const string ToolHandleMaxDur = "tinkeredToolHandleMaxDurability";
        public const string ToolBinding = "tinkeredToolBinding";
        public const string ToolBindingCurrentDur = "tinkeredToolBindingDurability";
        public const string ToolBindingMaxDur = "tinkeredToolBindingMaxDurability";

        public const string GripChanceToDamage = "gripChanceToDamage";
        public const string SpeedBonus = "speedBonus";
        public const string Drawback = "haftDrawback";

        public const string TotalHonedPercentSinceLastUse = "totalPercentHoned";

        //Using attributes as flags, if they exist on a tool, it means that flag is set
        public const string BrokeWhileSharpening = "haftBrokeToolWhileSharpening";
        public const string WhetstoneInUse = "haftWhetstoneInUse"; //Also used to carry the lastInterval value!
        public const string WhetstoneDoneSharpen = "haftWhetstoneDoneSharpen";
        public const string GrindstoneInUse = "haftGrindstoneInUse"; //Also used to carry the lastInterval value!
        public const string PartBeingCrafted = "partBeingCrafted";

        //The Attributes for the Part items themselves, used for Tool Heads and Handles currently. Try not to set these on a completed tool by mistake, use the specific above ones!
        public const string ToolPartCurrentDur = "toolPartCurrentDurability";
        public const string ToolPartMaxDur = "toolPartMaxDurability";

        //Attributes to control the addons to a handle, and the tool as a whole itself. Will be stored on the handle, and referenced for generating the renderer as well as stats. Important these are saved.
        public const string HandleStatTag = "toolHandleTag";
        public const string HandleGripTag = "toolHandleGripTag";
        public const string HandleTreatmentTag = "toolHandleTreatmentTag"; //Tags stay on the base item that have them and don't need to be moved to the crafted tool. They will have their stats transferred instead upwards.
        public const string HandleMaterialTag = "handleMaterialTag"; //What the handle is made of - a wood like "oak" or a metal like "steel". Set at craft time: from the supportbeam blank for a wood handle, from the ingot for a metal one. Keys a MaterialStats entry.
        public const string LegacyHandleWoodTag = "toolHandleWoodTag"; //The name HandleMaterialTag used to carry, back when a handle could only be wood. Read only for migration; never written.
        public const string GripAdhesiveTag = "gripAdhesiveTag"; //Which adhesive a grip was backed with, if any. Stamped on the grip stack rather than being a part of its own, so every grip that exists or is ever added inherits the mechanic without a parallel set of part defines.
        public const string PartReadyToBlue = "partReadyToBlue"; //Set once a handle has cooled below working heat. Bluing needs a part that was cooled first and then deliberately brought back to ~300C, not one that simply never left the forge.
        public const string PartWetTreatment = "partHasWetTreatment"; //Both a flag and holds the full time the treatment goes for.
        public const string DisposeMeNowPleaseTag = "disposeMeNowThisShouldntExist"; //Set the copy stack in the first Transition tick to this tag, to make retreiving it easier. It should regen this if it's somehow lost as well!

        // -- Render Data AttributeTree stuffs! --
        public const string ModularMultiPartDataTree = "modularMultiPartRenderData"; //This is a TreeAttribute that will contain more Trees of the respective parts (Or sub-shapes, for instance grips on a handle!). When added to a tool, the string tag for each part is that part's name. IE: Head, Handle or Binding, this will be set by the tool's behavior during OnCrafting.
        public const string BundleHasGenericParts = "bundleHasGenericParts"; //A flag attribute for Bundles only! Signals that the tool crafted from this shouldn't contain any multi-part rendering data.
        public const string ModularPartDataTree = "modularPartRenderData"; //This TreeAttribute is solely on individual parts to make retreieving them easier and consistant! This simply contains the Data entries organized below, and is also set and updated during OnCrafting!

        public const string ModularPartRotationX = "partRotationX";
        public const string ModularPartRotationY = "partRotationY";
        public const string ModularPartRotationZ = "partRotationZ";
        public const string ModularPartOffsetX = "partOffsetX";
        public const string ModularPartOffsetY = "partOffsetY";
        public const string ModularPartOffsetZ = "partOffsetZ";
        public const string ShapeOverrideAppendTag = "shapeOverrideAppendTag";

        public const string ModularPartShapeIndex = "partShapeIndex"; //This will just contain a string for the dictionary entry holding the part in the cache.
        public const string ModularPartTextureTree = "partTextures"; //This is another TreeAttribute that contains entries of the respective Shape's codes for the various textures in it, and the texture entries.
                                                                     //To help handle 'overlay' textures, find the intended entry to be overlayed, and then append a ++ to the end of the texture path, and afterwards add the overlay path. This might be what that one Texture handling class was looking for?
        public const string ModularPartHeadName = "head";
        public const string ModularPartHandleName = "handle"; //Making these constants so that they can be referenced all over the place to keep things consistant.
        public const string ModularPartBindingName = "binding"; //This is just the key for adding these respective parts to a MultiPartDataTree
        public const string ModularPartGripName = "grip";

        //Temp Attributes! Ones not intended to be saved to the item forever, and instead are used in the TempAttributes tree on the itemstack. It seems like the Temp Attributes get cleaned every time a slot is marked dirty.
        public const string HaftMeshID = "haftMeshrefID";

        // -- Vanilla Attribute Consts --
        //While these are not attributes created by the mod, I figure it might be beneficial to give them the same treatment. Just make sure they stay updated with the base game!
        public const string Durability = "durability";
        public const string TransitionState = "transitionstate";
        public const string WorkPieceVoxels = "voxels";
        public const string WorkPieceSelectedRecipeID = "selectedRecipeId";

        // -- Slated for Removal later down the line! Only kept around for the purposes of checking if they still exist and fixing them! Do not use these anymore!
        public const string ToolHeadCurrentDur = "tinkeredToolHeadDurability";
        public const string ToolHeadMaxDur = "tinkeredToolHeadMaxDurability";
        public const string OldHandlePrefix = "old"; //Adding this here so that I can reference specifically this from here, and later on when removing all these bits of old code later, it'll be easy to find all the errors just by commenting out this bit!

        // -- This just helps to organize it in this file, and pile them into one easy constant to call. Generally for Smithing Plus's Compat and the forgettable attributes there when a Workpiece is made.
        public const string HaftForgettableAttributes = "," + ToolHead + "," + ToolSharpnessCurrent + "," + ToolSharpnessMax + "," + ToolHandle + "," + ToolHandleCurrentDur + "," + ToolHandleMaxDur + "," + ToolBinding + "," + ToolBindingCurrentDur + "," + ToolBindingMaxDur + "," + GripChanceToDamage + "," + SpeedBonus + "," + Drawback + "," + TotalHonedPercentSinceLastUse + "," + BrokeWhileSharpening + "," + ModularMultiPartDataTree + "," + ModularPartDataTree;
        public static readonly string[] HaftIgnoreAttributesArray = [ToolHead, ToolSharpnessCurrent, ToolSharpnessMax, ToolHandle, ToolHandleCurrentDur, ToolHandleMaxDur, ToolBinding, ToolBindingCurrentDur, ToolBindingMaxDur, GripChanceToDamage, SpeedBonus, Drawback, TotalHonedPercentSinceLastUse, BrokeWhileSharpening, ModularMultiPartDataTree, ModularPartDataTree];
    }
}
