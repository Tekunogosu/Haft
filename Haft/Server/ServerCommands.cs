using System.Collections.Generic;
using Haft.Client;
using Haft.Client.Behaviors;
using Haft.ToolTinkering;
using Haft.ToolTinkering.Behaviors;
using Haft.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace Haft.Server {
    public static class ServerCommands {

        public static List<string> ValidAttributeParts = new() { "head", "hand", "handle", "bind", "binding", "tool", "part", "sharp", "sharpness" };
        public static List<string> CurrentOrMax = new() { "cur", "current", "max" };

        //Some helpful debugging server commands to try and set the rotation and offset of various parts that compose a ModularPartRenderer.
        //Only usable on an item that has multiple 'part' shapes!
        public static void RegisterServerCommands(ICoreServerAPI sapi) {
            sapi.ChatCommands
                .Create("setMultiPartRenderingRotation")
                .WithAlias("smprr")
                .WithDescription("In-game tweaking of the Rotation on a held Multi-Part item. [Haft]")
                .RequiresPrivilege("controlserver")
                .WithArgs(sapi.ChatCommands.Parsers.Word("partKey"), sapi.ChatCommands.Parsers.Float("rotateX"), sapi.ChatCommands.Parsers.Float("rotateY"), sapi.ChatCommands.Parsers.Float("rotateZ"))
                .HandleWith(args => OnSetMultiPartRenderingRotation(sapi, args));
            sapi.ChatCommands
                .Create("setMultiPartRenderingOffset")
                .WithAlias("smpro")
                .WithDescription("In-game tweaking of the Offset on a held Multi-Part item. [Haft]")
                .RequiresPrivilege("controlserver")
                .WithArgs(sapi.ChatCommands.Parsers.Word("partKey"), sapi.ChatCommands.Parsers.Float("offsetX"), sapi.ChatCommands.Parsers.Float("offsetY"), sapi.ChatCommands.Parsers.Float("offsetZ"))
                .HandleWith(args => OnSetMultiPartRenderingOffset(sapi, args));
            sapi.ChatCommands
                .Create("changeHaftAttribute")
                .WithAlias("cta")
                .WithDescription("Easier command to handle the adjusting of important various durability attributes related to Haft. [Haft]")
                .RequiresPrivilege("controlserver")
                .WithArgs(sapi.ChatCommands.Parsers.Word("partTarget"), sapi.ChatCommands.Parsers.Word("currentOrMax"), sapi.ChatCommands.Parsers.Int("value"))
                .HandleWith(args => OnChangeHaftAttribute(sapi, args));
            sapi.ChatCommands
                .Create("printAttributesToLog")
                .WithAlias("pa")
                .WithDescription("Print the attributes of the currently held item to the log. [Haft]")
                .RequiresPrivilege("controlserver")
                .HandleWith(args => OnPrintAttributes(sapi, args));
            sapi.ChatCommands
                .Create("finishTransition")
                .WithAlias("ft")
                .WithDescription("Immediately complete the held item's transition - drying a bowstave, a treatment curing - as though its full time had passed. [Haft]")
                .RequiresPrivilege("controlserver")
                .HandleWith(args => OnFinishTransition(sapi, args));
        }

        private static TextCommandResult OnSetMultiPartRenderingRotation(ICoreServerAPI sapi, TextCommandCallingArgs args) {
            var heldItem = args.Caller.Player.InventoryManager.ActiveHotbarSlot?.Itemstack;
            if (heldItem == null) {
                return TextCommandResult.Error("Could not find an active hotbar, or a held item for the player running the command!");
            }
            if (!heldItem.Collectible.HasBehavior<ModularPartRenderingFromAttributes>()) {
                return TextCommandResult.Error("Item was found, but does not have the ModularPartRenderingFromAttributes Behavior! This is only for items using that, otherwise it has no effect.");
            }
            if (!heldItem.HasMultiPartRenderTree()) {
                return TextCommandResult.Error("Item has the intended behavior, but does not have a set Multi-Part Rendering Tree Attribute. Was it instantiated properly?");
            }

            var partKey = args[0] as string;
            var rotateX = args[1] as float? ?? 0;
            var rotateY = args[2] as float? ?? 0;
            var rotateZ = args[3] as float? ?? 0;

            var multiPartTree = heldItem.GetMultiPartRenderTree();
            if (!multiPartTree.HasAttribute(partKey)) {
                return TextCommandResult.Error("Could not find part with key '" + partKey + "' on this item's multiPartTree.");
            }

            var partTree = multiPartTree.GetPartAndTransformRenderTree(partKey);
            partTree.SetPartRotationX(rotateX);
            partTree.SetPartRotationY(rotateY);
            partTree.SetPartRotationZ(rotateZ);

            args.Caller.Player.InventoryManager.ActiveHotbarSlot.MarkDirty();
            return TextCommandResult.Success("Successfully set part '" + partKey + "' rotation to X: " + rotateX + ", Y: " + rotateY + ", Z: " + rotateZ);
        }

        private static TextCommandResult OnSetMultiPartRenderingOffset(ICoreServerAPI sapi, TextCommandCallingArgs args) {
            var heldItem = args.Caller.Player.InventoryManager.ActiveHotbarSlot?.Itemstack;
            if (heldItem == null) {
                return TextCommandResult.Error("Could not find an active hotbar, or a held item for the player running the command!");
            }
            if (!heldItem.Collectible.HasBehavior<ModularPartRenderingFromAttributes>()) {
                return TextCommandResult.Error("Item was found, but does not have the ModularPartRenderingFromAttributes Behavior! This is only for items using that, otherwise it has no effect.");
            }
            if (!heldItem.HasMultiPartRenderTree()) {
                return TextCommandResult.Error("Item has the intended behavior, but does not have a set Multi-Part Rendering Tree Attribute. Was it instantiated properly?");
            }

            var partKey = args[0] as string;
            var offsetX = args[1] as float? ?? 0;
            var offsetY = args[2] as float? ?? 0;
            var offsetZ = args[3] as float? ?? 0;

            var multiPartTree = heldItem.GetMultiPartRenderTree();
            if (!multiPartTree.HasAttribute(partKey)) {
                return TextCommandResult.Error("Could not find part with key '" + partKey + "' on this item's multiPartTree.");
            }

            var partTree = multiPartTree.GetPartAndTransformRenderTree(partKey);
            partTree.SetPartOffsetX(offsetX);
            partTree.SetPartOffsetY(offsetY);
            partTree.SetPartOffsetZ(offsetZ);

            args.Caller.Player.InventoryManager.ActiveHotbarSlot.MarkDirty();
            return TextCommandResult.Success("Successfully set part '" + partKey + "' offset to X: " + offsetX + ", Y: " + offsetY + ", Z: " + offsetZ);
        }

        private static TextCommandResult OnChangeHaftAttribute(ICoreServerAPI sapi, TextCommandCallingArgs args) {
            var heldItem = args.Caller.Player.InventoryManager.ActiveHotbarSlot?.Itemstack;
            if (heldItem == null) {
                return TextCommandResult.Error("Could not find an active hotbar, or a held item for the player running the command!");
            }
            if (!heldItem.Collectible.HasBehavior<CollectibleBehaviorTinkeredTools>() && !heldItem.Collectible.HasBehavior<CollectibleBehaviorSmithedTools>() && !TinkeringUtility.IsValidHead(heldItem) && !heldItem.Collectible.HasBehavior<CollectibleBehaviorToolPartWithHealth>()) {
                return TextCommandResult.Error("Held Item is not a valid item with a Haft Behavior. Skipping adding or changing any attributes to avoid stacking problems!");
            }

            var partTarget = args[0] as string;
            var currentOrMax = args[1] as string;
            int value = args[2] as int? ?? 1;

            if (!ValidAttributeParts.Contains(partTarget)) {
                return TextCommandResult.Error("Could not parse the part to target. Please use something like \"head\", \"handle\", \"binding\", \"tool\", \"part\", \"sharp\"...");
            }
            if (!CurrentOrMax.Contains(currentOrMax)) {
                return TextCommandResult.Error("Could not parse current or max. Please enter either \"current\" or \"max\".");
            }

            if (heldItem.Collectible.HasBehavior<CollectibleBehaviorTinkeredTools>()) {
                switch (partTarget) {
                    case "head":
                        if (currentOrMax == "max") {
                            return TextCommandResult.Error("Cannot set the tool's max head durability! To edit this, a Json Patch is needed that changes the vanilla durability, or the Head Mult config will need adjusting.");
                        } else {
                            heldItem.SetToolheadCurrentDurability(value);
                        }
                        args.Caller.Player.InventoryManager.ActiveHotbarSlot.MarkDirty();
                        return TextCommandResult.Success("Set held tool's " + currentOrMax + " " + partTarget + " durability to " + value);
                    case "sharp":
                    case "sharpness":
                        if (currentOrMax == "max") {
                            heldItem.SetToolMaxSharpness(value);
                        } else {
                            heldItem.SetToolCurrentSharpness(value);
                        }
                        args.Caller.Player.InventoryManager.ActiveHotbarSlot.MarkDirty();
                        return TextCommandResult.Success("Set held tool's " + currentOrMax + " sharpness to " + value);
                    case "hand":
                    case "handle":
                        if (currentOrMax == "max") {
                            heldItem.SetToolhandleMaxDurability(value);
                        } else {
                            heldItem.SetToolhandleCurrentDurability(value);
                        }
                        args.Caller.Player.InventoryManager.ActiveHotbarSlot.MarkDirty();
                        return TextCommandResult.Success("Set held tool's " + currentOrMax + " " + partTarget + " durability to " + value);
                    case "bind":
                    case "binding":
                        if (currentOrMax == "max") {
                            heldItem.SetToolbindingMaxDurability(value);
                        } else {
                            heldItem.SetToolbindingCurrentDurability(value);
                        }
                        args.Caller.Player.InventoryManager.ActiveHotbarSlot.MarkDirty();
                        return TextCommandResult.Success("Set held tool's " + currentOrMax + " " + partTarget + " durability to " + value);
                    default:
                        return TextCommandResult.Error("That type of part is not valid for the held item, will avoid setting any attributes. Try using \"head\", \"sharp\", \"sharpness\", \"hand\", \"handle\", \"bind\" or \"binding\" instead!");
                }
            } else if (heldItem.Collectible.HasBehavior<CollectibleBehaviorSmithedTools>()) {
                switch (partTarget) {
                    case "tool":
                        if (currentOrMax == "max") {
                            return TextCommandResult.Error("Cannot set the tool's max durability! To edit this, a Json Patch is needed that changes the vanilla durability, or the Head Mult config will need adjusting.");
                        } else {
                            heldItem.SetSmithedDurability(value);
                        }
                        args.Caller.Player.InventoryManager.ActiveHotbarSlot.MarkDirty();
                        return TextCommandResult.Success("Set held tool's " + currentOrMax + " " + partTarget + " durability to " + value);
                    case "sharp":
                    case "sharpness":
                        if (currentOrMax == "max") {
                            heldItem.SetToolMaxSharpness(value);
                        } else {
                            heldItem.SetToolCurrentSharpness(value);
                        }
                        args.Caller.Player.InventoryManager.ActiveHotbarSlot.MarkDirty();
                        return TextCommandResult.Success("Set held tool's " + currentOrMax + " sharpness to " + value);
                    default:
                        return TextCommandResult.Error("That type of part is not valid for the held item, will avoid setting any attributes. Try using \"tool\", \"sharp\" or \"sharpness\" instead!");
                }
            } else if (TinkeringUtility.IsValidHead(heldItem)) {
                switch (partTarget) {
                    case "head":
                    case "part":
                        if (currentOrMax == "max") {
                            heldItem.SetPartMaxDurability(value);
                        } else {
                            heldItem.SetPartCurrentDurability(value);
                        }
                        args.Caller.Player.InventoryManager.ActiveHotbarSlot.MarkDirty();
                        return TextCommandResult.Success("Set held parts's " + currentOrMax + " durability to " + value);
                    case "sharp":
                    case "sharpness":
                        if (currentOrMax == "max") {
                            heldItem.SetPartMaxSharpness(value);
                        } else {
                            heldItem.SetPartCurrentSharpness(value);
                        }
                        args.Caller.Player.InventoryManager.ActiveHotbarSlot.MarkDirty();
                        return TextCommandResult.Success("Set held parts's " + currentOrMax + " sharpness to " + value);
                    default:
                        return TextCommandResult.Error("That type of part is not valid for the held item, will avoid setting any attributes. Try using \"head\", \"part\", \"sharp\" or \"sharpness\" instead!");
                }
            } else { //This should catch all regular Tool Parts With Health, and not those with sharpness as well.
                switch (partTarget) {
                    case "part":
                        if (currentOrMax == "max") {
                            heldItem.SetPartMaxDurability(value);
                        } else {
                            heldItem.SetPartCurrentDurability(value);
                        }
                        args.Caller.Player.InventoryManager.ActiveHotbarSlot.MarkDirty();
                        return TextCommandResult.Success("Set held parts's " + currentOrMax + " durability to " + value);
                    default:
                        return TextCommandResult.Error("That type of part is not valid for the held item, will avoid setting any attributes in case of a mistake. The valid option for this part is simply \"part\".");
                }
            }
        }

        private static TextCommandResult OnPrintAttributes(ICoreServerAPI sapi, TextCommandCallingArgs args) {
            var heldItem = args.Caller.Player.InventoryManager.ActiveHotbarSlot?.Itemstack;
            if (heldItem == null) {
                return TextCommandResult.Error("Could not find an active hotbar, or a held item for the player running the command!");
            }

            var treeString = RecursivelyPrintAttributes(heldItem.Attributes, 0, "");
            treeString += "\n-- End of Attributes Tree --";
            HaftModSystem.Logger.Debug("-- Printing attributes for " + heldItem.Collectible.Code + " --\n" + treeString);

            return TextCommandResult.Success("Attributes printed to Debug Log.");
        }

        //Completes the held stack's transition now instead of waiting out its real duration - 168 hours for a bowstave
        //to dry, which is otherwise the slowest possible way to test what a dried stave comes out carrying.
        //
        //Deliberately drives the REAL transition rather than building the transitioned stack itself: it asks the
        //collectible for its transitionable properties and calls OnTransitionNow, which is the same call the drying
        //tick makes. Constructing the result here instead would test this command rather than the code that runs in a
        //normal game, and the bugs worth catching - a behavior dropping an attribute, a stack size being lost - live
        //exactly in the path this way exercises.
        private static TextCommandResult OnFinishTransition(ICoreServerAPI sapi, TextCommandCallingArgs args) {
            var slot = args.Caller.Player.InventoryManager.ActiveHotbarSlot;
            if (slot?.Itemstack == null) {
                return TextCommandResult.Error("Could not find an active hotbar, or a held item for the player running the command!");
            }

            var heldItem = slot.Itemstack;
            var allProps = heldItem.Collectible.GetTransitionableProperties(sapi.World, heldItem, args.Caller.Entity);
            if (allProps == null || allProps.Length == 0) {
                return TextCommandResult.Error("Held item '" + heldItem.Collectible.Code + "' has no transitionable properties, so there is nothing for it to become. Try a raw bowstave, or a part carrying a wet treatment.");
            }

            //Only ever the first: a stack with several transitions has them in priority order, and the first is the
            //one the drying tick would reach as well.
            var props = allProps[0];
            var beforeCode = heldItem.Collectible.Code.ToString();
            var beforeSize = heldItem.StackSize;

            var transitioned = heldItem.Collectible.OnTransitionNow(slot, props);
            if (transitioned == null) {
                return TextCommandResult.Error("Held item '" + beforeCode + "' returned no stack from its " + props.Type + " transition.");
            }

            //The stack comes back from a clone of an already-resolved one, so this is normally a no-op. Guarded the
            //way vanilla guards its own calls, for the path where a behavior returns a stack it built from scratch.
            if (transitioned.Collectible == null) {
                transitioned.ResolveBlockOrItem(sapi.World);
            }

            slot.Itemstack = transitioned;
            slot.MarkDirty();

            //Plain prose with single quotes, matching every other command's result in this file. Chat renders VTML,
            //so a message is markup: an arrow written as -> has its > read as a closing tag delimiter and logs
            //"Found closing tag char > but no tag was opened" on every redraw of the scrollback.
            return TextCommandResult.Success(
                "Completed the " + props.Type + " transition on '" + beforeCode + "' x" + beforeSize +
                ", becoming '" + transitioned.Collectible.Code + "' x" + transitioned.StackSize +
                (transitioned.HasLimbMaterialTag() ? " with a limb of " + transitioned.GetLimbMaterialTag() : " with no limb material"));
        }

        private static string RecursivelyPrintAttributes(ITreeAttribute tree, int depth, string treeString) {
            foreach (var entry in tree) {
                for (int i = 0; i < depth; i++) {
                    treeString += "  ";
                }
                treeString += entry.Key + ": ";

                var value = tree.GetTreeAttribute(entry.Key);
                if (value != null) {
                    treeString += "\n";
                    treeString += RecursivelyPrintAttributes(value, depth + 1, "");
                } else {
                    var val = entry.Value.GetValue();
                    treeString += val.ToString() + "\n";
                }
            }

            return treeString;
        }
    }
}
