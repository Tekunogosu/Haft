using Vintagestory.API.Common.Entities;
using Vintagestory.API.Common;
using Toolsmith.Utils;

namespace Toolsmith.ToolTinkering.Drawbacks {
    public static class DrawbackUtility {

        //Drawbacks are not implemented: the roll happens and logs, but nothing is applied to the tool yet.
        //
        //The intended shape, for when they are: a Drawback becomes a class behind an interface carrying its id, the
        //tool tags it can apply to, whether it is minor or major, which majors a minor can worsen into, a roll weight
        //so the nastier ones can be made rarer, and its effect on the tool's performance. They are stored on the
        //stack under a Drawback attribute tree keyed by that id, which doubles as the lang key.

        //Check and see if a drawback is rolled and then have it applied. Returns true if drawback is applied, false if not!
        public static bool TryChanceForDrawback(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, float sharpnessPercent) {
            if (sharpnessPercent >= 0.8) {
                return false;
            }

            if (sharpnessPercent <= 0) {
                ApplyRandomDrawback(world, byEntity, itemslot, sharpnessPercent);
                return true;
            }

            bool shouldApplyDrawback = MathUtility.ShouldChanceForDefectCurve(world, sharpnessPercent, itemslot.Itemstack.GetToolMaxSharpness());
            if (shouldApplyDrawback) {
                ApplyRandomDrawback(world, byEntity, itemslot, sharpnessPercent);
            }
            return shouldApplyDrawback;
        }

        //Check for valid drawbacks for this tool type given, then try rolling for one to apply it.
        public static void ApplyRandomDrawback(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, float sharpnessPercent) {
            if (HasDrawback(itemslot.Itemstack)) {
                if (ToolsmithModSystem.Config.DebugMessages) {
                    ToolsmithModSystem.Logger.Warning("A Tool should have had a Drawback Worsened!!!");
                }
            } else {
                if (ToolsmithModSystem.Config.DebugMessages) {
                    ToolsmithModSystem.Logger.Warning("A Tool should have had a Drawback applied!");
                }
            }
        }

        public static bool HasDrawback(ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(ToolsmithAttributes.Drawback);
        }
    }
}
