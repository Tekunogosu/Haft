using System;
using System.Collections.Generic;
using Haft.Client;
using Haft.Compat;
using Haft.Config;
using Haft.ToolTinkering;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Haft.Utils {
    //Accessors for the PART items - heads, handles, bindings, grips, treatments and bow limbs - before and after
    //they are assembled into a tool. See ItemStackExtensions.cs for why these are split by what the stack is.
    public static partial class ItemStackExtensions {
        // -- ItemStack Extensions for the Part items --
        public static void SetPartCurrentDurability(this ItemStack itemStack, int durability) {
            itemStack.Attributes.SetInt(HaftAttributes.ToolPartCurrentDur, durability);
        }

        public static int GetPartCurrentDurability(this ItemStack itemStack) {
            return itemStack.Attributes.GetInt(HaftAttributes.ToolPartCurrentDur, itemStack.GetPartMaxDurability());
        }

        public static bool HasPartCurrentDurability(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.ToolPartCurrentDur);
        }

        public static void RemovePartCurrentDurability(this ItemStack itemStack) {
            itemStack.Attributes.RemoveAttribute(HaftAttributes.ToolPartCurrentDur);
        }

        public static void SetPartMaxDurability(this ItemStack itemStack, int durability) {
            itemStack.Attributes.SetInt(HaftAttributes.ToolPartMaxDur, durability);
        }

        //Zero, not a stand-in number, when a part has recorded no maximum. A literal default here read as a real
        //maximum to every caller that divides by it, which is how a head whose true maximum was higher produced a
        //remaining-HP fraction above 1.0 and, multiplied back out, a part stronger than a new one. Callers that can
        //act on missing data ask HasPartMaxDurability first; ResetHeadStats is what repairs a part that has none.
        public static int GetPartMaxDurability(this ItemStack itemStack) {
            return itemStack.Attributes.GetInt(HaftAttributes.ToolPartMaxDur, 0);
        }

        public static bool HasPartMaxDurability(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.ToolPartMaxDur);
        }

        public static void RemovePartMaxDurability(this ItemStack itemStack) {
            itemStack.Attributes.RemoveAttribute(HaftAttributes.ToolPartMaxDur);
        }

        public static float GetPartRemainingHPPercent(this ItemStack itemStack) { //Since by design of wanting to let handles and bindings scale to the Tool's vanilla HP values, this is needed to 'hold' over the damage it sustained during use if it survived.
            var currentDur = itemStack.GetPartCurrentDurability();
            var maxDur = itemStack.GetPartMaxDurability();
            if (currentDur > 0 && maxDur > 0) {
                return ((float)currentDur / (float)maxDur);
            }
            return 0.0f;
        }

        public static void SetPartCurrentSharpness(this ItemStack itemStack, int sharpness) { //Only to be used on the tool heads, this doesn't call the reset, because can only handle that when it has a base durability, and the heads are just a regular item.
            itemStack.Attributes.SetInt(HaftAttributes.ToolSharpnessCurrent, sharpness);
        }

        public static int GetPartCurrentSharpness(this ItemStack itemStack) {
            if (!itemStack.Attributes.HasAttribute(HaftAttributes.ToolSharpnessCurrent)) {
                if (!itemStack.Attributes.HasAttribute(HaftAttributes.ToolPartCurrentDur)) {
                    itemStack.ResetHeadStats();
                } else {
                    itemStack.ResetHeadSharpness();
                }
            }

            return itemStack.Attributes.GetInt(HaftAttributes.ToolSharpnessCurrent);
        }

        public static void SetPartMaxSharpness(this ItemStack itemStack, int sharpness) {
            itemStack.Attributes.SetInt(HaftAttributes.ToolSharpnessMax, sharpness);
        }

        public static int GetPartMaxSharpness(this ItemStack itemStack) {
            if (!itemStack.Attributes.HasAttribute(HaftAttributes.ToolSharpnessMax)) {
                itemStack.ResetHeadSharpness();
            }

            return itemStack.Attributes.GetInt(HaftAttributes.ToolSharpnessMax);
        }

        public static float GetPartRemainingSharpnessPercent(this ItemStack itemStack) {
            var currentSharp = itemStack.GetPartCurrentSharpness();
            var maxSharp = itemStack.GetPartMaxSharpness();
            if (currentSharp >= 0 && maxSharp > 0) {
                return ((float)currentSharp / (float)maxSharp);
            }
            return 0.0f;
        }

        public static void SetPartBeingCrafted(this ItemStack itemStack) {
            if (itemStack.Attributes == null) {
                itemStack.Attributes = new TreeAttribute();
            }
            itemStack.Attributes.SetBool(HaftAttributes.PartBeingCrafted, true);
        }

        public static void ClearPartBeingCrafted(this ItemStack itemStack) {
            itemStack.Attributes?.RemoveAttribute(HaftAttributes.PartBeingCrafted);
        }

        public static bool PartBeingCrafted(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.PartBeingCrafted);
        }

        public static void SetTotalHoneValue(this ItemStack itemStack, float honed) {
            itemStack.Attributes.SetFloat(HaftAttributes.TotalHonedPercentSinceLastUse, honed);
        }

        public static float GetTotalHoneValue(this ItemStack itemStack) {
            return itemStack.Attributes.GetFloat(HaftAttributes.TotalHonedPercentSinceLastUse); //If unset, it means it should get the 'first time honing' bonus of no durability loss.
        }

        public static bool HasTotalHoneValue(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.TotalHonedPercentSinceLastUse);
        }

        public static void SetGrindstoneInUse(this ItemStack itemStack, float lastInterval) {
            if (itemStack.Attributes == null) {
                itemStack.Attributes = new TreeAttribute();
            }
            itemStack.Attributes.SetFloat(HaftAttributes.GrindstoneInUse, lastInterval);
        }

        public static float GetGrindstoneInUse(this ItemStack itemStack) {
            return itemStack.Attributes.GetFloat(HaftAttributes.GrindstoneInUse, 0.0f);
        }

        public static void ClearGrindstoneInUse(this ItemStack itemStack) {
            itemStack.Attributes?.RemoveAttribute(HaftAttributes.GrindstoneInUse);
        }

        public static void SetWhetstoneInUse(this ItemStack itemStack, float lastInterval) {
            if (itemStack.Attributes == null) {
                itemStack.Attributes = new TreeAttribute();
            }
            itemStack.Attributes.SetFloat(HaftAttributes.WhetstoneInUse, lastInterval);
        }

        public static float GetWhetstoneInUse(this ItemStack itemStack) {
            return itemStack.Attributes.GetFloat(HaftAttributes.WhetstoneInUse);
        }

        public static void ClearWhetstoneInUse(this ItemStack itemStack) {
            itemStack.Attributes?.RemoveAttribute(HaftAttributes.WhetstoneInUse);
        }

        public static bool WhetstoneInUse(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.WhetstoneInUse);
        }

        public static void SetWhetstoneDoneSharpen(this ItemStack itemStack) {
            if (itemStack.Attributes == null) {
                itemStack.Attributes = new TreeAttribute();
            }
            itemStack.Attributes.SetBool(HaftAttributes.WhetstoneDoneSharpen, true);
        }

        public static void ClearWhetstoneDoneSharpen(this ItemStack itemStack) {
            itemStack.Attributes?.RemoveAttribute(HaftAttributes.WhetstoneDoneSharpen);
        }

        public static bool WhetstoneDoneSharpen(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.WhetstoneDoneSharpen);
        }

        public static void ResetHeadStats(this ItemStack itemStack) {
            if (itemStack.Attributes == null) {
                itemStack.Attributes = new TreeAttribute();
            }

            int maxDur;
            int maxSharp;

            if (RecipeRegisterModSystem.TinkerToolGridRecipes?.Count > 0) {
                CollectibleObject firstTool;
                var success = RecipeRegisterModSystem.TinkerToolGridRecipes.TryGetValue(itemStack.Collectible.Code, out firstTool);
                if (success) {
                    var baseDur = firstTool.GetBaseMaxDurability(new ItemStack(firstTool));
                    maxDur = (int)(baseDur * HaftModSystem.Config.HeadDurabilityMult);

                    itemStack.SetPartMaxDurability(maxDur);
                    itemStack.SetPartCurrentDurability(maxDur);

                    maxSharp = ScientificSmithyCompat.CalculateMaxSharpness(itemStack, baseDur);
                    itemStack.SetPartMaxSharpness(maxSharp);
                    itemStack.SetPartCurrentSharpness((int)(itemStack.Collectible.StartingSharpnessMult() * maxSharp));
                    return;
                }
            }

            //No recipe to read a tool's durability from, so the head is rated from the metal it is made of - the same
            //number it would get if it were crafted now. A head with no entry in the material table keeps whatever it
            //already recorded, and the flat base only stands in for one that has recorded nothing at all.
            var resetMaterialStats = itemStack.GetHeadMaterialStats();
            if (resetMaterialStats != null) {
                maxDur = TinkeringUtility.ScaleToHeadDurability((int)HaftPartStatsHelpers.CalculateHeadDurability(resetMaterialStats));
            } else {
                maxDur = itemStack.HasPartMaxDurability()
                    ? itemStack.GetPartMaxDurability()
                    : TinkeringUtility.ScaleToHeadDurability(HaftConstants.PartDurabilityBase);
            }
            itemStack.SetPartMaxDurability(maxDur);
            itemStack.SetPartCurrentDurability(maxDur);

            maxSharp = ScientificSmithyCompat.CalculateMaxSharpness(itemStack, (int)(maxDur / HaftModSystem.Config.HeadDurabilityMult));
            itemStack.SetPartMaxSharpness(maxSharp);
            itemStack.SetPartCurrentSharpness((int)(itemStack.Collectible.StartingSharpnessMult() * maxSharp));
        }

        public static void ResetHeadSharpness(this ItemStack itemStack) {
            var maxDur = itemStack.GetPartMaxDurability();
            int maxSharp = ScientificSmithyCompat.CalculateMaxSharpness(itemStack, (int)(maxDur / HaftModSystem.Config.HeadDurabilityMult));

            itemStack.SetPartMaxSharpness(maxSharp);
            itemStack.SetPartCurrentSharpness((int)(itemStack.Collectible.StartingSharpnessMult() * maxSharp));
        }

        public static void SetHandleStatTag(this ItemStack itemStack, string tag) {
            itemStack.Attributes.SetString(HaftAttributes.HandleStatTag, tag);
        }

        public static string GetHandleStatTag(this ItemStack itemStack) {
            return itemStack.Attributes.GetString(HaftAttributes.HandleStatTag);
        }

        public static bool HasHandleStatTag(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.HandleStatTag);
        }

        public static void RemoveHandleStatTag(this ItemStack itemStack) {
            itemStack.Attributes.RemoveAttribute(HaftAttributes.HandleStatTag);
        }

        //Everything a handle offers to a part being applied to it: the tags its own part define carries, plus the
        //tags of whatever it is made of. Gathering both is what lets a rule be written against the material without
        //splitting the handle parts per material - one "handle" part still covers all thirteen woods, and an oak
        //handle and a pine handle answer differently because their materials do.
        public static HashSet<string> GetHandleProvidedTags(this ItemStack handle) {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (handle?.Collectible?.Code != null) {
                var part = HaftModSystem.Stats.BaseHandleParts.TryGetValue(handle.Collectible.Code.Path);
                if (part?.providesTags != null) {
                    tags.AddRange(part.providesTags);
                }
            }

            var materialStats = handle.GetHandleMaterialStats();
            if (materialStats?.providesTags != null) {
                tags.AddRange(materialStats.providesTags);
            }

            //A finish the handle already carries is itself something a later treatment can require. Bluing is the
            //case that needs it: the oxide layer is porous on its own and is traditionally sealed with oil, so oil
            //asks for a blued surface rather than for bare metal.
            if (handle != null && handle.HasHandleTreatmentTag()) {
                tags.Add(handle.GetHandleTreatmentTag());
            }

            return tags;
        }

        //Resolves the material stats for a handle, in one place, because two callers need the same answer: the
        //material the stack records, else oak - the fallback a stick, a bone or a crude handle has always taken,
        //having no material of its own, and the one a handle that never passed through crafting takes as well.
        public static MaterialStatDefines GetHandleMaterialStats(this ItemStack handle) {
            if (handle.HasHandleMaterialTag()) {
                var tag = handle.GetHandleMaterialTag();
                if (tag != null && HaftModSystem.Stats.MaterialStats.ContainsKey(tag)) {
                    return HaftModSystem.Stats.MaterialStats.Get(tag);
                }
            }

            return HaftModSystem.Stats.MaterialStats.Get(HaftConstants.DefaultMaterialStatKey);
        }

        //The head counterpart to GetHandleMaterialStats. A head carries no material tag of its own: the metal it was
        //smithed from is already in its variant, so that is what gets looked up rather than a second attribute that
        //could disagree with it.
        //
        //Returns null rather than falling back to oak, which is the opposite of the handle's rule above and
        //deliberate. A stick genuinely has no material and oak stands in for its numbers; a bone or flint head is not
        //a soft metal, and rating it as one would hand it a durability derived from a table it does not belong to.
        //Null lets the caller keep the vanilla-derived durability such a head already has.
        public static MaterialStatDefines GetHeadMaterialStats(this ItemStack head) {
            var metal = head?.Collectible?.GetMetalMaterial();
            if (metal != null && HaftModSystem.Stats.MaterialStats.ContainsKey(metal)) {
                return HaftModSystem.Stats.MaterialStats.Get(metal);
            }

            return null;
        }

        public static void SetHandleMaterialTag(this ItemStack itemStack, string tag) {
            itemStack.Attributes.SetString(HaftAttributes.HandleMaterialTag, tag);
        }

        //Reads the material, migrating a handle saved under the old toolHandleWoodTag name as it goes. The migration
        //lives here rather than in a separate pass so that every caller gets it without having to remember to ask:
        //a handle from an older save is rewritten the first time anything looks at it, and a handle that has already
        //been migrated costs one HasAttribute check.
        public static string GetHandleMaterialTag(this ItemStack itemStack) {
            if (itemStack.Attributes.HasAttribute(HaftAttributes.HandleMaterialTag)) {
                return itemStack.Attributes.GetString(HaftAttributes.HandleMaterialTag);
            }

            if (itemStack.Attributes.HasAttribute(HaftAttributes.LegacyHandleWoodTag)) {
                var legacy = itemStack.Attributes.GetString(HaftAttributes.LegacyHandleWoodTag);
                itemStack.Attributes.SetString(HaftAttributes.HandleMaterialTag, legacy);
                itemStack.Attributes.RemoveAttribute(HaftAttributes.LegacyHandleWoodTag);
                return legacy;
            }

            return null;
        }

        public static bool HasHandleMaterialTag(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.HandleMaterialTag) ||
                   itemStack.Attributes.HasAttribute(HaftAttributes.LegacyHandleWoodTag);
        }

        public static void RemoveHandleMaterialTag(this ItemStack itemStack) {
            itemStack.Attributes.RemoveAttribute(HaftAttributes.HandleMaterialTag);
            itemStack.Attributes.RemoveAttribute(HaftAttributes.LegacyHandleWoodTag);
        }

        public static void SetLimbMaterialTag(this ItemStack itemStack, string tag) {
            itemStack.Attributes.SetString(HaftAttributes.LimbMaterialTag, tag);
        }

        public static string GetLimbMaterialTag(this ItemStack itemStack) {
            return itemStack.Attributes.GetString(HaftAttributes.LimbMaterialTag);
        }

        public static bool HasLimbMaterialTag(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.LimbMaterialTag);
        }

        //How well the bow was built, read off the item code. Null for anything that is not a parted bow, which is
        //every vanilla bow and every unrelated projectile the shooting patches also see.
        public static BowStatDefines GetBowTierStats(this ItemStack bow) {
            var variant = bow?.Collectible?.Variant?["type"];
            if (variant == null || !HaftConstants.BowStatKeyByVariant.TryGetValue(variant, out var tierKey)) {
                return null;
            }

            return HaftModSystem.Stats.BowStats.Get(tierKey);
        }

        //Whether this bow records the wood it was built from. A crude bow is lashed together from sticks and cordage,
        //so there is no species to name and none is asked for - the same reason a crude handle has no wood variant.
        //A bow whose tier is unknown is treated as having a wood axis, because the tiers that do not are the named
        //exception rather than the default.
        public static bool BowHasWoodAxis(this ItemStack bow) {
            return bow.GetBowTierStats()?.hasWoodAxis != false;
        }

        //The limb counterpart to GetHandleMaterialStats. It shares the oak fallback only where the crude handle
        //already does - a tier with no wood axis has no species to record, so oak stands in for its numbers exactly
        //as it does for a stick, a bone or a crude handle.
        //
        //Everywhere else the fallback is deliberately NOT taken: a limb with no material on a tier that should have
        //one is a bow that was never crafted from a stave, and rating it as oak would quietly hand a creative-spawned
        //bow a real draw weight. Returning null lets the caller say "unknown" instead, the way the handle tooltip
        //already does.
        public static MaterialStatDefines GetLimbMaterialStats(this ItemStack limb) {
            if (!limb.HasLimbMaterialTag()) {
                return limb.BowHasWoodAxis() ? null : HaftModSystem.Stats.MaterialStats.Get(HaftConstants.DefaultMaterialStatKey);
            }

            var tag = limb.GetLimbMaterialTag();
            return tag != null ? HaftModSystem.Stats.MaterialStats.Get(tag) : null;
        }

        //The grip stats for a bow, or null when it has none. A bow with no grip recorded is a bare riser rather than
        //a missing value, so the caller reads null as "contributes nothing" rather than falling back to a default.
        public static GripStatDefines GetBowGripStats(this ItemStack bow) {
            if (bow == null || !bow.HasHandleGripTag()) {
                return null;
            }

            return HaftModSystem.Stats.GripStats.Get(bow.GetHandleGripTag());
        }

        //The treatment applied to this stack, falling back to the untreated entry rather than to null.
        //
        //The distinction from GetBowGripStats above is deliberate and is the reason these are separate methods
        //rather than one generic one: an absent GRIP means a bare riser, which contributes nothing, while an absent
        //TREATMENT means the untreated entry, which is a real row in the table with real numbers. One is "no such
        //part", the other is "the default part".
        public static TreatmentStatDefines GetTreatmentStatsOrDefault(this ItemStack itemStack) {
            return HaftModSystem.Stats.TreatmentStats.Get(
                itemStack?.HasHandleTreatmentTag() == true
                    ? itemStack.GetHandleTreatmentTag()
                    : HaftConstants.DefaultTreatmentTag);
        }

        //The grip on this stack, falling back to the plain entry. For a BOW use GetBowGripStats, which reads an
        //absent grip as nothing at all; a handle always has a grip even if that grip is "plain".
        public static GripStatDefines GetGripStatsOrDefault(this ItemStack itemStack) {
            return HaftModSystem.Stats.GripStats.Get(
                itemStack?.HasHandleGripTag() == true
                    ? itemStack.GetHandleGripTag()
                    : HaftConstants.DefaultGripTag);
        }

        public static void SetHandleGripTag(this ItemStack itemStack, string tag) {
            itemStack.Attributes.SetString(HaftAttributes.HandleGripTag, tag);
        }

        public static string GetHandleGripTag(this ItemStack itemStack) {
            return itemStack.Attributes.GetString(HaftAttributes.HandleGripTag);
        }

        public static bool HasHandleGripTag(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.HandleGripTag);
        }

        public static void SetHandleTreatmentTag(this ItemStack itemStack, string tag) {
            itemStack.Attributes.SetString(HaftAttributes.HandleTreatmentTag, tag);
        }

        public static string GetHandleTreatmentTag(this ItemStack itemStack) {
            return itemStack.Attributes.GetString(HaftAttributes.HandleTreatmentTag);
        }

        public static bool HasHandleTreatmentTag(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.HandleTreatmentTag);
        }

        public static void SetGripAdhesiveTag(this ItemStack itemStack, string tag) {
            itemStack.Attributes.SetString(HaftAttributes.GripAdhesiveTag, tag);
        }

        public static string GetGripAdhesiveTag(this ItemStack itemStack) {
            return itemStack.Attributes.GetString(HaftAttributes.GripAdhesiveTag);
        }

        public static bool HasGripAdhesiveTag(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.GripAdhesiveTag);
        }

        //What a grip offers a handle it is being attached to: its part define's tags, plus "adhesive-backed" when it
        //has actually been backed with one. The adhesive is a stack attribute rather than a part, so a rule about it
        //has to be answered from the stack - reading only the part define would miss it entirely.
        public static HashSet<string> GetGripProvidedTags(this ItemStack grip) {
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (grip?.Collectible?.Code != null) {
                var part = HaftModSystem.Stats.GripParts.TryGetValue(grip.Collectible.Code.Path);
                if (part?.providesTags != null) {
                    tags.AddRange(part.providesTags);
                }
            }

            if (grip.HasGripAdhesiveTag()) {
                tags.Add(HaftConstants.AdhesiveBackedTag);
            }

            return tags;
        }

        public static void SetReadyToBlue(this ItemStack itemStack) {
            itemStack.Attributes.SetBool(HaftAttributes.PartReadyToBlue, true);
        }

        public static bool IsReadyToBlue(this ItemStack itemStack) {
            return itemStack.Attributes.GetBool(HaftAttributes.PartReadyToBlue, false);
        }

        public static void SetWetTreatment(this ItemStack itemStack, int hours) {
            itemStack.Attributes.SetInt(HaftAttributes.PartWetTreatment, hours);
        }

        public static int GetWetTreatment(this ItemStack itemStack) {
            return itemStack.Attributes.GetInt(HaftAttributes.PartWetTreatment);
        }

        public static bool HasWetTreatment(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.PartWetTreatment);
        }

        public static void RemoveWetTreatment(this ItemStack itemStack) {
            itemStack.Attributes.RemoveAttribute(HaftAttributes.PartWetTreatment);
            itemStack.Attributes.RemoveAttribute(HaftAttributes.TransitionState);

            var multiPartTree = itemStack.GetMultiPartRenderTree();
            var partAndTransformTree = multiPartTree.GetPartAndTransformRenderTree(HaftAttributes.ModularPartHandleName);
            var renderTree = partAndTransformTree.GetPartRenderTree();
            var textureTree = renderTree.GetPartTextureTree();
            foreach (var texture in textureTree) {
                if (texture.Key.Contains("-overlay")) {
                    textureTree.RemoveAttribute(texture.Key);
                }
            }
        }

        public static void SetDisposeMeNowPlease(this ItemStack itemStack) {
            if (itemStack.Attributes == null) {
                itemStack.Attributes = new TreeAttribute();
            }
            itemStack.Attributes.SetBool(HaftAttributes.DisposeMeNowPleaseTag, true);
        }

        public static bool HasDisposeMeNowPlease(this ItemStack itemStack) {
            return itemStack.Attributes.HasAttribute(HaftAttributes.DisposeMeNowPleaseTag);
        }

    }
}
