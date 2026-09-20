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
    //Helpers that ask something of ANY stack or collectible, with no assumption that it is a tool or a part. See
    //ItemStackExtensions.cs for why these are split out.
    public static partial class ItemStackExtensions {
        // -- More Generic ItemStack extensions or helper methods intended to handle items/collectibleobjects --
        public static void AddBehavior<T>(this CollectibleObject collectibleObject) where T : CollectibleBehavior {
            if (collectibleObject == null || collectibleObject.HasBehavior<T>()) {
                return;
            }

            try {
                var addedBehavior = (T)Activator.CreateInstance(typeof(T), collectibleObject);
                collectibleObject.CollectibleBehaviors = collectibleObject.CollectibleBehaviors.Append(addedBehavior);
            } catch (Exception ex) {
                HaftModSystem.Logger.Error("Something went wrong attempting to add a behavior to the provided Collectable with code: " + collectibleObject.Code + "\nIf this isn't an intended Tool or Part, try adding it to the blacklist to avoid this in the future!");
                HaftModSystem.Logger.Error(ex);
            }
        }

        //How sharp a freshly made edge starts out, as a share of its maximum. Metal takes and holds a keener edge
        //than bone, flint or obsidian, so the two are banded rather than sharing one figure.
        public static float StartingSharpnessMult(this CollectibleObject collectibleObject) {
            return collectibleObject.IsCraftableMetal() ? HaftConstants.StartingSharpnessMult : HaftConstants.NonMetalStartingSharpnessMult;
        }

        //Checks if a given CollectableObject is made of metal
        public static bool IsCraftableMetal(this CollectibleObject collectibleObject, ICoreAPI api = null) {
            return collectibleObject.GetMetalItem(api) != null;
        }

        //Will return null if it is not a metal material!
        public static string GetMetalItem(this CollectibleObject collectibleObject, ICoreAPI api = null) {
            api ??= HaftModSystem.Api;
            var ingotItem = api?.World.GetItem(new AssetLocation("game:ingot-" + collectibleObject.GetMetalMaterial()));
            return ingotItem?.Variant["metal"] ?? ingotItem?.Variant["material"];
        }

        //Will return null if the given CollectibleObject does not have a 'metal' or 'material' variant typing!
        public static string GetMetalMaterial(this CollectibleObject collectibleObject) {
            return collectibleObject.Variant["metal"] ?? collectibleObject.Variant["material"];
        }

        //Since we know what the Head Durability Mult is, lets hook into GetMaxDurability for compat with other mods, let them do their things, THEN divide by the known HeadDurabilityMult to get the changed base value back.
        public static int GetBaseMaxDurability(this CollectibleObject collectibleObject, ItemStack itemStack) {
            return (int)((double)itemStack.Collectible.GetMaxDurability(itemStack) / HaftModSystem.Config.HeadDurabilityMult);
        }
    }
}
