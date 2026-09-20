using System;
using System.Linq;
using System.Text;
using Haft.Compat;
using Haft.Config;
using Haft.Utils;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Haft.ToolTinkering.Items;
using Haft.ToolTinkering.Behaviors;
using Haft.Client;
using Vintagestory.GameContent;
using Vintagestory.API.Config;
using Haft.ToolTinkering.Drawbacks;
using Vintagestory.API.Datastructures;
using Newtonsoft.Json.Linq;

namespace Haft.ToolTinkering {
    //Identifying which part of a tool a stack is, and the three steps of honing one: reading the durability and
    //sharpness off it, running a tick of the hone, and writing the result back.
    public static partial class TinkeringUtility {
        public static EnumToolPart IsAnyToolPart(ItemStack itemstack, IWorldAccessor world) {
            if (world.Side.IsServer() && HaftModSystem.IgnoreCodes.Count > 0 && HaftModSystem.IgnoreCodes.Contains(itemstack.Collectible.Code.ToString())) {
                return EnumToolPart.None;
            } else if (IsValidHead(itemstack)) {
                return EnumToolPart.Head;
            } else if (IsValidHandle(itemstack)) {
                return EnumToolPart.Handle;
            } else if (IsBindingItem(itemstack)) { //Asking what a stack IS, so an absent one must not come back as a binding.
                return EnumToolPart.Binding;
            }

            return EnumToolPart.None;
        }

        //Where a honeable item keeps its durability and sharpness. The three kinds store them under different
        //attributes, so every step of the honing path has to know which it is holding; None means it cannot be honed.
        public enum EnumSharpenTarget {
            None,
            TinkeredTool,
            SmithedTool,
            ToolHead
        }

        //One honeable item's durability and sharpness, read out of whichever attributes its kind keeps them in.
        //
        //The mapping from kind to accessor pair lives in Read and Write below and nowhere else. It used to be
        //written out at each step of the honing path - reading, ticking, writing, and again when a tooltip asked
        //whether an item needed honing - so a kind stored somewhere new had four places to teach rather than one,
        //and each site re-derived the same answer with its own copy of the branch.
        public readonly struct PartDurability {

            public int CurrentDurability { get; init; }
            public int MaxDurability { get; init; }
            public int CurrentSharpness { get; init; }
            public int MaxSharpness { get; init; }

            //What fraction of its durability the item has left. Zero when nothing usable was recorded, matching
            //GetPartRemainingHPPercent, so a caller cannot divide by an absent maximum.
            public float DurabilityPercent =>
                CurrentDurability > 0 && MaxDurability > 0 ? (float)CurrentDurability / MaxDurability : 0.0f;

            public bool NeedsSharpening => CurrentSharpness < MaxSharpness && CurrentDurability > 0;

            public static PartDurability Read(ItemStack item, EnumSharpenTarget target) {
                switch (target) {
                    case EnumSharpenTarget.TinkeredTool:
                        //A tool with no proper head recorded keeps its durability and reads as having no sharpness to
                        //work on. Something has already gone wrong and been logged; leaving sharpness at zero stops
                        //honing without the write-back then treating a zeroed durability as a tool honed to breaking.
                        if (item.HasPlaceholderHead()) {
                            return new PartDurability {
                                CurrentDurability = item.GetToolheadCurrentDurability(),
                                MaxDurability = item.GetToolheadMaxDurability()
                            };
                        }

                        //Read so their getters initialize a tool that has never had its parts written; the values
                        //themselves are not wanted here.
                        item.GetToolhandleCurrentDurability();
                        item.GetToolbindingCurrentDurability();
                        return new PartDurability {
                            CurrentDurability = item.GetToolheadCurrentDurability(),
                            MaxDurability = item.GetToolheadMaxDurability(),
                            CurrentSharpness = item.GetToolCurrentSharpness(),
                            MaxSharpness = item.GetToolMaxSharpness()
                        };

                    case EnumSharpenTarget.SmithedTool:
                        return new PartDurability {
                            CurrentDurability = item.GetSmithedDurability(),
                            MaxDurability = item.GetSmithedMaxDurability(),
                            CurrentSharpness = item.GetToolCurrentSharpness(),
                            MaxSharpness = item.GetToolMaxSharpness()
                        };

                    case EnumSharpenTarget.ToolHead:
                        return new PartDurability {
                            CurrentDurability = item.GetPartCurrentDurability(),
                            MaxDurability = item.GetPartMaxDurability(),
                            CurrentSharpness = item.GetPartCurrentSharpness(),
                            MaxSharpness = item.GetPartMaxSharpness()
                        };

                    default:
                        return default;
                }
            }

            //Writes back only what honing changes. An item honed to nothing keeps one point rather than breaking on
            //the stone, which is where that floor has always been applied.
            public static void Write(ItemStack item, EnumSharpenTarget target, int currentDurability, int currentSharpness) {
                if (currentDurability <= 0) {
                    currentDurability = 1;
                }

                switch (target) {
                    case EnumSharpenTarget.TinkeredTool:
                        item.SetToolheadCurrentDurability(currentDurability);
                        item.SetToolCurrentSharpness(currentSharpness);
                        break;

                    case EnumSharpenTarget.SmithedTool:
                        item.SetSmithedDurability(currentDurability);
                        item.SetToolCurrentSharpness(currentSharpness);
                        break;

                    default:
                        item.SetPartCurrentDurability(currentDurability);
                        item.SetPartCurrentSharpness(currentSharpness);
                        break;
                }
            }
        }

        //This checks if it is a valid repair tool as well as if it is a fully tinkered tool or if it is just a tool's head, since the durabilities are stored under different attributes
        public static EnumSharpenTarget IsValidSharpenTool(CollectibleObject item, IWorldAccessor world) {
            if (world.Side.IsServer() && HaftModSystem.IgnoreCodes.Count > 0 && HaftModSystem.IgnoreCodes.Contains(item.Code.ToString())) { //First check if the ignore list has any entries, and ensure this one isn't on it. Likely means something got improperly given the Behavior on init.
                return EnumSharpenTarget.None;
            } else if (item.HasBehavior<CollectibleBehaviorTinkeredTools>()) { //This one stores it under 'tinkeredToolHead' durability
                if (!item.HasBehavior<CollectibleBehaviorToolBlunt>()) {
                    return EnumSharpenTarget.TinkeredTool;
                }
            } else if (item.HasBehavior<CollectibleBehaviorSmithedTools>()) { //And this one just uses the regular durability values since it's just a single solid tool, no parts
                if (!item.HasBehavior<CollectibleBehaviorToolBlunt>()) {
                    return EnumSharpenTarget.SmithedTool;
                }
            } else if (item.HasBehavior<CollectibleBehaviorToolHead>()) { //While this stores it as just 'toolPartDurability', since not every part will be a head, but every head will have this behavior
                if (!item.HasBehavior<CollectibleBehaviorToolBlunt>()) {
                    return EnumSharpenTarget.ToolHead;
                }
            }

            return EnumSharpenTarget.None;
        }

        public static bool IsDeconstructableTool(CollectibleObject item, IWorldAccessor world) {
            if (world.Side.IsServer() && HaftModSystem.IgnoreCodes.Count > 0 && HaftModSystem.IgnoreCodes.Contains(item.Code.ToString())) { //First check if the ignore list has any entries, and ensure this one isn't on it. Likely means something got improperly given the Behavior on init.
                return false;
            } else if (item.HasBehavior<CollectibleBehaviorTinkeredTools>()) { //This one stores it under 'tinkeredToolHead' durability
                return true;
            } else if (item is ItemWorkItem) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Whether a tooltip should tell the player this item still has its free first honing to spend.
        /// </summary>
        /// <remarks>
        /// Having never been honed is not on its own worth saying: a head sharpened as a loose part keeps its free
        /// honing, because SetResultsOfSharpening only records a hone value once the free one is spent, and the
        /// craft that fits it to a handle carries over a value that was never written. The result is a tool that is
        /// already sharp and still advertises honing it does not need. Asking whether it is actually dull as well
        /// is what separates the two, and ToolOrHeadNeedsSharpening is the same test the whetstone and grindstone
        /// use to decide when to stop, rather than a second opinion about the same question.
        /// </remarks>
        public static bool ShouldOfferFreeHoning(ItemStack item, IWorldAccessor world) {
            return !item.HasTotalHoneValue() && ToolOrHeadNeedsSharpening(item, world);
        }

        public static bool ToolOrHeadNeedsSharpening(ItemStack item, IWorldAccessor world, EntityAgent byEntity = null) {
            var toolType = IsValidSharpenTool(item.Collectible, world);
            if (toolType == EnumSharpenTarget.None) {
                return false;
            }

            var readout = PartDurability.Read(item, toolType);

            //Honing a tool to the point of breaking it is worth telling the player about rather than letting the
            //stone quietly stop.
            if (byEntity != null && readout.DurabilityPercent <= HaftConstants.DoNotSharpenBelowPercent) {
                if (byEntity.Api.Side.IsClient()) {
                    (byEntity.Api as ICoreClientAPI).TriggerIngameError(item, "HoningStopBeforeBreak", Lang.Get("honing-cutoff-message"));
                }
            }

            return readout.NeedsSharpening && readout.DurabilityPercent > HaftConstants.DoNotSharpenBelowPercent;
        }

        public static bool TryWhetstoneSharpening(ref float lastInterval, float secondsUsed, ItemSlot slot, EntityAgent byEntity) {
            if (byEntity.World.Side.IsServer()) {
                var deltaLastTick = secondsUsed - lastInterval;

                if (deltaLastTick >= HaftConstants.SharpenInterval) { //Try not to repair EVERY single tick to space it out some. Cause of this, repair (configurable %) durability each time so it doesn't take forever.
                    var whetstone = WhetstoneInOffhand(byEntity);

                    if (whetstone != null && !slot.Empty) { //If the offhand is still a Whetstone, sharpen! Otherwise break out of this entirely and end the action.
                        var isTool = IsValidSharpenTool(slot.Itemstack.Collectible, byEntity.World);
                        whetstone.HandleSharpenTick(secondsUsed, slot, byEntity.LeftHandItemSlot, byEntity, isTool);
                    } else {
                        return false;
                    }

                    lastInterval = MathUtility.FloorToNearestMult(secondsUsed, HaftConstants.SharpenInterval);

                    if (byEntity.LeftHandItemSlot.Empty || byEntity.LeftHandItemSlot.Itemstack.WhetstoneDoneSharpen()) {
                        return false; //End the interaction when it doesn't need sharpening anymore
                    }
                }
            }

            return true;
        }

        //Honing runs in three steps - read the values, apply one tick, write them back - so the grindstone and the
        //whetstone share the arithmetic and differ only in what drives the ticks.
        public static void RecieveDurabilitiesAndSharpness(ref int curDur, ref int maxDur, ref int curSharp, ref int maxSharp, ref float totalHoned, ItemStack item, EnumSharpenTarget isTool) {
            var readout = PartDurability.Read(item, isTool);
            curDur = readout.CurrentDurability;
            maxDur = readout.MaxDurability;
            curSharp = readout.CurrentSharpness;
            maxSharp = readout.MaxSharpness;

            if (item.HasTotalHoneValue()) {
                totalHoned = item.GetTotalHoneValue();
            }
        }

        public static void ActualSharpenTick(ref int curDur, ref int curSharp, int maxSharp, ref float totalSharpnessHoned, bool firstHoning, EntityAgent byEntity) {
            if (curSharp < maxSharp && curDur > 0) {
                float percent = 1.0f;
                if (HaftModSystem.Config.GrindstoneSharpenPerTick > 0.0 && HaftModSystem.Config.GrindstoneSharpenPerTick <= 100.0) {
                    percent = ((float)HaftModSystem.Config.GrindstoneSharpenPerTick / 100f);
                }
                int amountSharpened = (int)Math.Ceiling(percent * maxSharp);

                bool damageDurability = true;
                bool doubleDamage = false;
                double damageMultFromLinear = 0.0;

                if (!firstHoning && HaftModSystem.Config.ShouldHoningDamageHead && HaftModSystem.Config.HoningDamageMult > 0) {
                    if (totalSharpnessHoned > 0.5) {
                        damageDurability = byEntity.World.Rand.NextDouble() < 0.05;
                    } else if (totalSharpnessHoned > 0.4 && totalSharpnessHoned <= 0.5) {
                        damageDurability = MathUtility.ShouldDamageFromSharpening(byEntity.World, totalSharpnessHoned);
                    } else if (totalSharpnessHoned > 0.2 && totalSharpnessHoned <= 0.4) {
                        damageMultFromLinear = MathUtility.GetLinearDamageMult(totalSharpnessHoned);
                    } else {
                        doubleDamage = true;
                    }
                } else {
                    damageDurability = false;
                }

                if (damageDurability) {
                    int amountToDamage = (int)(amountSharpened * HaftModSystem.Config.HoningDamageMult);
                    if (doubleDamage) {
                        amountToDamage *= 2;
                    } else if (damageMultFromLinear > 1.0) {
                        amountToDamage = (int)((double)amountToDamage * damageMultFromLinear);
                    }

                    if (curDur - amountToDamage <= 0) {
                        var overflowDamage = Math.Abs(curDur - amountToDamage) + 1;
                        amountToDamage += overflowDamage;

                        if (overflowDamage > 0 && doubleDamage) {
                            overflowDamage /= 2;
                        } else if (damageMultFromLinear > 1.0) {
                            overflowDamage = (int)((double)amountToDamage / damageMultFromLinear);
                        }
                        amountSharpened -= overflowDamage;
                    }

                    curDur -= amountToDamage;
                }

                curSharp += amountSharpened;
                if (curSharp >= maxSharp) {
                    curSharp = maxSharp;
                    totalSharpnessHoned = 1.0f;
                } else {
                    totalSharpnessHoned += percent;
                }
            }
        }

        //Honing never breaks what is being honed: a durability that reaches zero here is floored at 1 instead.
        //The three targets keep the same values under different attributes, which is the only thing that differs.
        //
        //A hone value is recorded only once the free first honing has been spent, so an unset value is what marks a
        //tool as still holding that free hone.
        public static void SetResultsOfSharpening(int curDur, int curSharp, float totalSharpnessHoned, bool firstHoning, ItemStack item, EnumSharpenTarget isTool) {
            PartDurability.Write(item, isTool, curDur, curSharp);

            if (!firstHoning) {
                item.SetTotalHoneValue(totalSharpnessHoned);
            }
        }
    }
}
