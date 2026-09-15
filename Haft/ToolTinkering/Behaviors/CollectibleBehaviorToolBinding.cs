using System;
using System.Text;
using Haft.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Util;

namespace Haft.ToolTinkering.Behaviors {
    //Marks a collectible as a tool binding, which is how every binding check in the mod identifies one, and adds the
    //binding's stats to its tooltip.
    public class CollectibleBehaviorToolBinding : CollectibleBehavior {
        public CollectibleBehaviorToolBinding(CollectibleObject collObj) : base(collObj) {

        }

        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo) {
            if (world != null && world.Side.IsClient()) {
                dsc.AppendLine(Lang.Get("toolbindingdirections"));
                if (HaftModSystem.BindingTiers != null) {
                    dsc.AppendLine(Lang.Get("toolbindingtier", HaftModSystem.BindingTiers.Get(inSlot.Itemstack.Collectible.Code.Path)));
                }

                var bindingPart = HaftModSystem.Stats.BindingParts.Get(inSlot.Itemstack.Collectible.Code.Path);
                if (bindingPart == null) {
                    return;
                }
                var bindingStats = HaftModSystem.Stats.BindingStats.Get(bindingPart.bindingStatTag);
                if (bindingStats != null) {
                    var totalMult = bindingStats.baseHPfactor * (1 + bindingStats.selfHPBonus);
                    dsc.AppendLine("");
                    dsc.AppendLine(Lang.Get("toolbindingtotalmult", StringHelpers.ColorForMultiplier(totalMult), float.Truncate(totalMult * 100) / 100));
                    dsc.AppendLine(Lang.Get("toolbindinghandlebonus", StringHelpers.ColorForBonus(bindingStats.handleHPBonus), Math.Round(bindingStats.handleHPBonus * 100)));
                    dsc.AppendLine(Lang.Get("toolbindingrecoverychance", Math.Round(bindingStats.recoveryPercent * 100)));
                    if (bindingStats.isMetal) {
                        dsc.AppendLine(Lang.Get("toolbindingmetaldrops"));
                    }
                }
            }
        }
    }
}
