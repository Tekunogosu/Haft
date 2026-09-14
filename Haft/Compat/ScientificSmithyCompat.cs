using ScientificSmithy.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Haft.Compat {

    //Everything Haft needs to know about ScientificSmithy lives here, so the rest of the codebase can ask for a
    //sharpness value without also knowing which mod supplied the numbers behind it. ScientificSmithy stamps a stats
    //tree onto a stack when it is smithed; where that tree is present its hardness and toughness decide how sharp the
    //result can get, and where it is absent Haft's own config multiplier does.
    public static class ScientificSmithyCompat {

        /// <summary>
        /// The maximum sharpness a stack should have, read from ScientificSmithy's smithing stats when it has them
        /// and derived from the durability it is built on when it does not.
        /// </summary>
        /// <param name="statsSource">
        /// The stack carrying the ScientificSmithy stats tree. Usually the stack being written to, but a tool being
        /// crafted takes its sharpness from the head going into it rather than from itself, so the two are separate
        /// parameters - passing the wrong one there silently gives every crafted tool the same sharpness.
        /// </param>
        /// <param name="baseDurability">
        /// The durability this sharpness derives from, used both as the toughness fallback and as the base of the
        /// no-ScientificSmithy calculation. Callers working from a part's max durability pass that divided by the head
        /// durability multiplier, so the number handed in is always the tool's own base rather than the scaled one.
        /// </param>
        public static int CalculateMaxSharpness(ItemStack statsSource, int baseDurability) {
            if (statsSource?.Attributes != null && statsSource.Attributes.HasAttribute(ScientificSmithyAttr.StatsAttr)) {
                ITreeAttribute stats = statsSource.Attributes.GetTreeAttribute(ScientificSmithyAttr.StatsAttr);
                float sharpMult = stats.GetFloat(ScientificSmithyAttr.HardnessMultAttr, (float)HaftModSystem.Config.SharpnessMult);
                int halfTough = stats.GetInt(ScientificSmithyAttr.HalfToughAttr, baseDurability);
                return (int)(sharpMult * halfTough);
            }

            return (int)(baseDurability * HaftModSystem.Config.SharpnessMult);
        }

        //Carries ScientificSmithy's stress and strain values from a tool onto the work item a reforge produces, so a
        //reforged tool remembers what the old one had been through. Only meaningful while that mod is loaded; callers
        //check IsModEnabled first.
        public static void HandleStressStrainTransfer(ItemStack outStack, ItemStack inStack, ICoreAPI api) {
            outStack.TransferStressStrainAttr(inStack, api);
        }
    }
}
