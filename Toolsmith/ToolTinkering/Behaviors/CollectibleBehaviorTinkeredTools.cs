using ItemRarity.Rarities;
using System;
using System.Linq;
using System.Text;
using Toolsmith.Client;
using Toolsmith.Compat;
using Toolsmith.Config;
using Toolsmith.ToolTinkering.Drawbacks;
using Toolsmith.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Toolsmith.ToolTinkering.Behaviors {
    public class CollectibleBehaviorTinkeredTools : CollectibleBehavior {

        public CollectibleBehaviorTinkeredTools(CollectibleObject collObj) : base(collObj) {

        }

        //Clientside only. Shows each part's durability in place of the single vanilla durability line.
        public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo) {
            //A stack in a handbook, creative or trader inventory has no attribute data worth reading, and writing
            //defaults onto one would stamp stats on a display item.
            if (TinkeringUtility.ShouldNotAccessStats(inSlot) || ToolsmithModSystem.IgnoreCodes.Count > 0 && ToolsmithModSystem.IgnoreCodes.Contains(inSlot.Itemstack.Collectible.Code.ToString())) {
                return;
            }

            var curHeadDur = inSlot.Itemstack.GetToolheadCurrentDurability();
            var maxHeadDur = inSlot.Itemstack.GetToolheadMaxDurability();
            var curHandleDur = inSlot.Itemstack.GetToolhandleCurrentDurability();
            var maxHandleDur = inSlot.Itemstack.GetToolhandleMaxDurability();
            var curBindingDur = inSlot.Itemstack.GetToolbindingCurrentDurability();
            var maxBindingDur = inSlot.Itemstack.GetToolbindingMaxDurability();
            var curSharp = inSlot.Itemstack.GetToolCurrentSharpness();
            var maxSharp = inSlot.Itemstack.GetToolMaxSharpness();
            
            bool didResetParts = false;

            //A negative durability means the tool has no part stats yet - spawned in creative, or already in a save
            //when the mod was added. The reset is safe on either side; the slot is marked dirty afterwards so the
            //client's copy reaches the server.
            if (curHeadDur < 0) {
                inSlot.Itemstack.ResetNullHead(world);
                curHeadDur = inSlot.Itemstack.GetToolheadCurrentDurability();
                didResetParts = true;
            }
            if (maxHandleDur < 0 || maxBindingDur < 0) {
                inSlot.Itemstack.ResetNullHandleOrBinding(world);
                curHandleDur = inSlot.Itemstack.GetToolhandleCurrentDurability();
                maxHandleDur = inSlot.Itemstack.GetToolhandleMaxDurability();
                curBindingDur = inSlot.Itemstack.GetToolbindingCurrentDurability();
                maxBindingDur = inSlot.Itemstack.GetToolbindingMaxDurability();
                didResetParts = true;
            }

            //Built in a copy and swapped in at the end, since the vanilla durability line has to be found by index
            //and removed before the part lines can go in its place.
            StringBuilder workingDsc = new StringBuilder();
            workingDsc.Append(dsc);
            int startIndex = 0;
            int endIndex = 0;

            StringHelpers.FindTooltipVanillaDurabilityLine(ref startIndex, ref endIndex, workingDsc, world, withDebugInfo);

            if (endIndex < workingDsc.Length) {
                workingDsc.Remove(startIndex, endIndex - startIndex + 1); //Remove the durability line
            }

            if (!inSlot.Itemstack.Collectible.HasBehavior<CollectibleBehaviorToolBlunt>()) {
                if (inSlot.Itemstack.HasTotalHoneValue() && inSlot.Itemstack.GetTotalHoneValue() > 0 && inSlot.Itemstack.GetTotalHoneValue() < 1) {
                    workingDsc.AppendLine(Lang.Get("tinkeredtoolhoninginprogress"));
                } else if (TinkeringUtility.ShouldOfferFreeHoning(inSlot.Itemstack, world)) {
                    workingDsc.AppendLine(Lang.Get("tinkeredtoolfreehone"));
                }
            }
            workingDsc.Insert(startIndex, Lang.Get("toolbindingdurability", StringHelpers.ColorForDurability(curBindingDur, maxBindingDur), curBindingDur, maxBindingDur) + '\n'); //Insert in the part durabilities in the place of it
            workingDsc.Insert(startIndex, Lang.Get("toolhandledurability", StringHelpers.ColorForDurability(curHandleDur, maxHandleDur), curHandleDur, maxHandleDur) + '\n');
            workingDsc.Insert(startIndex, Lang.Get("toolheaddurability", StringHelpers.ColorForDurability(curHeadDur, maxHeadDur), curHeadDur, maxHeadDur) + '\n');
            if (!inSlot.Itemstack.Collectible.HasBehavior<CollectibleBehaviorToolBlunt>()) {
                workingDsc.Insert(startIndex, Lang.Get("toolsharpness", StringHelpers.ColorForDurability(curSharp, maxSharp), curSharp, maxSharp) + '\n');
            }

            //The wood is recorded on the handle the tool was built from, not on the tool, so it has to be read back
            //off that stored stack. GetToolhandleForData rather than GetToolhandle: this is a tooltip asking a
            //question, and the mutating version would write a handle onto any tool that had lost one.
            var storedHandle = inSlot.Itemstack.GetToolhandleForData();
            if (storedHandle != null && storedHandle.HasHandleMaterialTag()) {
                workingDsc.AppendLine(Lang.Get("toolhandlewood", Lang.Get("material-" + storedHandle.GetHandleMaterialTag())));
            }

            //A tool can be built without a binding at all, so a null here is an ordinary answer rather than missing
            //data. GetToolbinding already returns null instead of writing one back, so it is safe to ask from a tooltip.
            var storedBinding = inSlot.Itemstack.GetToolbinding();
            if (storedBinding != null) {
                var bindingPart = ToolsmithModSystem.Stats.BindingParts.Get(storedBinding.Collectible.Code.Path);
                if (bindingPart != null) {
                    var bindingStats = ToolsmithModSystem.Stats.BindingStats.Get(bindingPart.bindingStatTag);
                    if (bindingStats != null && bindingStats.langTag != "") {
                        workingDsc.AppendLine(Lang.Get("toolbindingmaterial", Lang.Get(bindingStats.langTag)));
                    }
                }
            }

            TinkeringUtility.ReplaceVanillaToolSpeedLines(inSlot, workingDsc);

            dsc.Clear();
            dsc.Append(workingDsc);
            if (didResetParts) {try {inSlot.MarkDirty();} catch (Exception e) {
                    ToolsmithModSystem.Logger.Warning("Toolsmith: Could not mark an itemslot dirty after fixing null tool part data (the slot likely belongs to a non-standard/virtual inventory, e.g. an auction house listing or handbook preview). This is expected and harmless in that case. Exception details: " + e.Message);
                }
            }
        }

        //Builds a tool's stats from the parts that went into it. The input slots hold at minimum a head and a handle
        //(which may be a plain stick) and may hold a binding. Their order is not fixed, so each part is identified by
        //its behavior rather than by position.
        public override void OnCreatedByCrafting(ItemSlot[] allInputslots, ItemSlot outputSlot, IRecipeBase byRecipe, ref EnumHandling bhHandling) {
            //First, figure out what actually went into the tool. Investigate the Inputs and look for the individual behaviors. This will find the parts!
            ItemStack headStack = null;
            ItemStack handleStack = null;
            ItemStack bindingStack = null;
            bool liquidBinding = false;
            ItemStack foundToolInput = null;

            bhHandling = EnumHandling.Handled;
            foreach (var itemSlot in allInputslots.Where(i => i.Itemstack != null)) { //Is it possible any slot could even be null here...? Like when a grid-craft is done. Better to be safe though?
                if (TinkeringUtility.IsValidHead(itemSlot.Itemstack)) { //If it has this behavior, found the tool head!
                    headStack = itemSlot.Itemstack.Clone();
                    headStack.StackSize = 1;
                } else if (TinkeringUtility.IsValidHandle(itemSlot.Itemstack)) { //If this one, then handle found!
                    handleStack = itemSlot.Itemstack.Clone();
                    handleStack.StackSize = 1;
                } else if (TinkeringUtility.IsValidBinding(itemSlot.Itemstack)) { //And finally the (possible) binding! This isn't garenteed though remember, the others are.
                    liquidBinding = itemSlot.Itemstack.Block as BlockLiquidContainerBase != null;
                    if (liquidBinding) {
                        //A liquid binding is not stored on the tool: only its stats carry over, since there is no
                        //item to hand back when the tool comes apart.
                        bindingStack = TinkeringUtility.GetBindingContent(itemSlot.Itemstack);
                    } else {
                        bindingStack = itemSlot.Itemstack.Clone();
                        bindingStack.StackSize = 1;
                    }
                } else if (itemSlot.Itemstack.Collectible.HasBehavior<CollectibleBehaviorTinkeredTools>() && itemSlot.Itemstack.Collectible.Code == outputSlot.Itemstack.Collectible.Code) {
                    foundToolInput = itemSlot.Itemstack.Clone();
                    foundToolInput.StackSize = 1;
                }
            }

            bool isHeadMetal = false;
            if (headStack == null && foundToolInput == null) { //I do hope nothing hits this. At least it'll likely get the candle backup.
                ToolsmithModSystem.Logger.Error("Somehow crafted a Tinker Tool with a recipe that could not find a head, nor tool to copy data from.\nThe tool in question is: " + outputSlot.Itemstack.Collectible.Code.ToString() + "\nAttempting to just reset the Tool Head instead. This might result in fallback data of a Candle being assigned.");
                outputSlot.Itemstack.ResetNullHead(ToolsmithModSystem.Api.World);
                headStack = outputSlot.Itemstack.GetToolhead();
            } else if (headStack == null && foundToolInput != null) {
                //A recipe consuming a finished tool and producing the same tool - Working Classes crafts a knife from
                //a knife and a whetstone. Every stat carries straight across rather than being recalculated.
                outputSlot.Itemstack.SetToolhead(foundToolInput.GetToolhead());
                outputSlot.Itemstack.SetToolheadCurrentDurability(foundToolInput.GetToolheadCurrentDurability());
                outputSlot.Itemstack.SetToolCurrentSharpness(foundToolInput.GetToolCurrentSharpness());
                outputSlot.Itemstack.SetToolMaxSharpness(foundToolInput.GetToolMaxSharpness());
                if (foundToolInput.HasTotalHoneValue()) {
                    outputSlot.Itemstack.SetTotalHoneValue(foundToolInput.GetTotalHoneValue());
                }
                outputSlot.Itemstack.SetToolhandle(foundToolInput.GetToolhandle());
                outputSlot.Itemstack.SetToolhandleCurrentDurability(foundToolInput.GetToolhandleCurrentDurability());
                outputSlot.Itemstack.SetToolhandleMaxDurability(foundToolInput.GetToolhandleMaxDurability());
                var foundToolInputBinding = foundToolInput.GetToolbinding();
                if (foundToolInputBinding != null) { //A tool built without a binding has none to carry over.
                    outputSlot.Itemstack.SetToolbinding(foundToolInputBinding);
                }
                outputSlot.Itemstack.SetToolbindingCurrentDurability(foundToolInput.GetToolbindingCurrentDurability());
                outputSlot.Itemstack.SetToolbindingMaxDurability(foundToolInput.GetToolbindingMaxDurability());
                outputSlot.Itemstack.SetSpeedBonus(foundToolInput.GetSpeedBonus());
                outputSlot.Itemstack.SetGripChanceToDamage(foundToolInput.GetGripChanceToDamage());
                return;
            } else {
                isHeadMetal = headStack.Collectible.IsCraftableMetal();
            }

            //Remove errant attribute data that might still be on the Tool Head like the temp. Vanilla crafting doesn't carry over the temp so it should be cleared before saving the head to the tool.
            if (headStack.Attributes != null) {
                if (headStack.Attributes.HasAttribute("temperature")) {
                    headStack.Attributes.RemoveAttribute("temperature");
                }
                if (ToolsmithModSystem.Api.ModLoader.IsModEnabled("smithingplus")) { //If Smithing Plus is found, clear it's errant data as well or else it will compound due to how it reassigns it when the head breaks.
                    if (headStack.Attributes.HasAttribute("repairedToolStack")) {
                        headStack.Attributes.RemoveAttribute("repairedToolStack");
                    }
                    if (headStack.Attributes.HasAttribute("repairSmith")) {
                        headStack.Attributes.RemoveAttribute("repairSmith");
                    }
                }
            }

            //Once all (up to) three possible parts are found, access the stats for the handle and binding! The Head doesn't need much done to it, it's the simplest to handle.
            HandlePartDefines handle;
            bool handleSuccess;
            if (handleStack != null) { //It probably shouldn't ever be the case it gets here and Handle is still null but hey.
                handleSuccess = ToolsmithModSystem.Stats.BaseHandleParts.TryGetValue(handleStack.Collectible.Code.Path, out handle);
            } else {
                handleSuccess = ToolsmithModSystem.Stats.BaseHandleParts.TryGetValue(ToolsmithConstants.DefaultHandlePartKey, out handle); //Probably shouldn't ever run into this, but just incase something does go wrong, this might prevent a crash - and default to a stick used. Maybe if configs are not configured right this could happen!
                handleStack = new ItemStack(ToolsmithModSystem.Api.World.GetItem(new AssetLocation(ToolsmithConstants.DefaultHandleCode)), 1);
            }
            if (!handleSuccess) {
                handle = ToolsmithModSystem.Stats.BaseHandleParts.First().Value;
            }

            if (headStack != null && handleStack != null && foundToolInput == null) {
                MultiPartRenderingHelpers.BuildToolRenderFromAllSeparateParts(outputSlot.Itemstack, headStack, handleStack, bindingStack);
            }

            BindingPartDefines binding;
            if (bindingStack != null) { //If there is a binding used, then get that one.
                if (bindingStack.Attributes != null) {
                    if (bindingStack.Attributes.HasAttribute("temperature")) {
                        bindingStack.Attributes.RemoveAttribute("temperature");
                    }
                }
                binding = ToolsmithModSystem.Stats.BindingParts.Get(bindingStack.Collectible.Code.Path);
            } else {
                binding = ToolsmithModSystem.Stats.BindingParts.Get(ToolsmithConstants.DefaultBindingPartKey);
            }
            var handleStats = ToolsmithModSystem.Stats.BaseHandleStats.Get(handle.handleStatTag);
            //A null binding part means none was used, which is a valid way to build a tool rather than missing data.
            var bindingStats = ToolsmithModSystem.Stats.BindingStats.Get(binding == null ? ToolsmithConstants.DefaultBindingStatKey : binding.bindingStatTag);
            var stats = ToolsmithPartStatsHelpers.ResolveHandleStats(handleStats, handleStack, bindingStats);

            //Runs before the first GetBaseMaxDurability call, since a compat mod may change what that returns.
            HandleExtraModCompat(allInputslots, outputSlot);

            var baseDur = outputSlot.Itemstack.Collectible.GetBaseMaxDurability(outputSlot.Itemstack);
            int headMaxDur = outputSlot.Itemstack.GetToolheadMaxDurability();
            //Read off the head rather than the tool being made: the head is what carries the smithing stats into the
            //finished tool, and reading the output stack here would give every tool the same sharpness.
            int maxSharpness = ScientificSmithyCompat.CalculateMaxSharpness(headStack, baseDur);

            var handleDur = ToolsmithPartStatsHelpers.CalculateHandleDurability(stats);
            var bindingDur = ToolsmithPartStatsHelpers.CalculateBindingDurability(stats);

            //Apply the end results of that to the tool/parts. Could the parts themselves actually hold the stats...? Eh. Might be faster to just directly apply them to the tool and then update the current HP when it breaks.
            //A zero percent means nothing has been recorded on the part yet, which is treated as full rather than as
            //a part with no durability left.
            var currentHeadPer = headStack.GetPartRemainingHPPercent();
            headStack.SetPartMaxDurability(headMaxDur);
            if (currentHeadPer <= 0) {
                currentHeadPer = 1.0f;
            }
            headStack.SetPartCurrentDurability((int)(headMaxDur * currentHeadPer));
            var currentHeadSharpPer = headStack.GetPartRemainingSharpnessPercent();
            headStack.SetPartMaxSharpness(maxSharpness);
            if (currentHeadSharpPer < 0) {
                if (isHeadMetal) {
                    currentHeadSharpPer = ToolsmithConstants.StartingSharpnessMult;
                } else {
                    currentHeadSharpPer = ToolsmithConstants.NonMetalStartingSharpnessMult;
                }
            }
            headStack.SetPartCurrentSharpness((int)(currentHeadSharpPer * maxSharpness));

            outputSlot.Itemstack.SetToolheadCurrentDurability((int)(headMaxDur * currentHeadPer));
            outputSlot.Itemstack.SetToolMaxSharpness(maxSharpness);
            outputSlot.Itemstack.SetToolCurrentSharpness((int)(maxSharpness * currentHeadSharpPer));
            if (headStack.HasTotalHoneValue()) {
                outputSlot.Itemstack.SetTotalHoneValue(headStack.GetTotalHoneValue());
            }

            var currentHandlePer = handleStack.GetPartRemainingHPPercent();
            handleStack.SetPartMaxDurability((int)handleDur);
            if (currentHandlePer <= 0) {
                currentHandlePer = 1.0f;
            }
            handleStack.SetPartCurrentDurability((int)(handleDur * currentHandlePer));
            outputSlot.Itemstack.SetToolhandleMaxDurability((int)handleDur);
            outputSlot.Itemstack.SetToolhandleCurrentDurability((int)(handleDur * currentHandlePer));

            outputSlot.Itemstack.SetToolbindingMaxDurability((int)bindingDur);
            outputSlot.Itemstack.SetToolbindingCurrentDurability((int)bindingDur);

            var speedBonus = ToolsmithPartStatsHelpers.CalculateSpeedBonus(stats);
            var gripChanceDamage = ToolsmithPartStatsHelpers.CalculateGripChanceToDamage(stats);
            outputSlot.Itemstack.SetSpeedBonus(speedBonus);
            outputSlot.Itemstack.SetGripChanceToDamage(gripChanceDamage);

            if (ToolsmithModSystem.Config.DebugMessages) {
                ToolsmithModSystem.Logger.Debug("Tool's durability is: " + baseDur);
                ToolsmithModSystem.Logger.Debug("Thus, the Tool Head's durability is: " + headMaxDur);
                ToolsmithModSystem.Logger.Debug("And the current Head Durability is: " + (int)(headMaxDur * currentHeadPer));
                ToolsmithModSystem.Logger.Debug("The tool's maximum sharpness is: " + maxSharpness);
                ToolsmithModSystem.Logger.Debug("This tool's current sharpness is: " + (int)(maxSharpness * currentHeadSharpPer));
                ToolsmithModSystem.Logger.Debug("Handle Max Durability: " + handleDur);
                ToolsmithModSystem.Logger.Debug("Handle Current Durability: " + (int)(handleDur * currentHandlePer));
                ToolsmithModSystem.Logger.Debug("Binding Durability: " + bindingDur);
                ToolsmithModSystem.Logger.Debug("Speed Bonus: " + speedBonus);
                ToolsmithModSystem.Logger.Debug("Chance To Damage: " + gripChanceDamage);
            }

            //Then don't forget to add the ItemStacks for the parts to the tool's attributes to retrieve later on damage/destruction!
            outputSlot.Itemstack.SetToolhead(headStack);
            outputSlot.Itemstack.SetToolhandle(handleStack);
            if (!liquidBinding && bindingStack != null) {
                outputSlot.Itemstack.SetToolbinding(bindingStack);
            }
        }

        private void HandleExtraModCompat(ItemSlot[] allInputslots, ItemSlot outputSlot) {
            if (ToolsmithModSystem.Api.ModLoader.IsModEnabled("xskills")) { //Copy over the Quality Attribute (if it exists) onto the output item so that the GetMaxDurability will account for it here!
                HandleXSkillsCompat(allInputslots, outputSlot);
            }

            if (ToolsmithModSystem.Api.ModLoader.IsModEnabled("itemrarity")) {
                HandleItemRarityCompat(allInputslots, outputSlot);
            }

            if (ToolsmithModSystem.Api.ModLoader.IsModEnabled("canjewelry")) {
                foreach (var input in allInputslots.Where(i => !i.Empty && TinkeringUtility.IsValidHead(i.Itemstack))) {
                    CanJewelryCompat.CheckAndHandleJewelryStatTransfer(input.Itemstack, outputSlot.Itemstack);
                    break;
                }
            }
        }

        private void HandleXSkillsCompat(ItemSlot[] allInputslots, ItemSlot outputSlot) {
            float quality = 0.0f;
            foreach (var input in allInputslots.Where(i => !i.Empty)) {
                if (input.Itemstack.Attributes.HasAttribute("quality")) {
                    quality = input.Itemstack.Attributes.GetFloat("quality");
                    break;
                }
            }

            if (quality > 0.0f) {
                outputSlot.Itemstack.Attributes.SetFloat("quality", quality); //Doesn't appear to double up or anything, thankfully!
            }
        }

        private void HandleItemRarityCompat(ItemSlot[] allInputslots, ItemSlot outputSlot) {
            var itemStack = outputSlot.Itemstack;
            if (!Rarity.IsSuitableFor(itemStack)) {
                return;
            }
            var rarity = Rarity.GetRandomRarity();
            Rarity.ApplyRarity(itemStack, rarity);
        }

        public override void OnDamageItem(IWorldAccessor world, Entity byEntity, ItemSlot itemslot, ref int amount, ref EnumHandling bhHandling) {
            if (world.Side.IsServer() && (ToolsmithModSystem.IgnoreCodes.Count == 0 || !ToolsmithModSystem.IgnoreCodes.Contains(itemslot.Itemstack.Collectible.Code.ToString()))) { //Lets just make sure it isn't on the ignore list.
                bhHandling = EnumHandling.PreventDefault; //Prevent all default damage handling serverside as long as it's not on the ignore list!

                ItemStack itemStack = itemslot.Itemstack;
                int remainingHeadDur = itemStack.GetToolheadCurrentDurability(); //Grab all the current durabilities of the parts!
                int remainingHandleDur = itemStack.GetToolhandleCurrentDurability(); //But none should be -1 already, if any are, it means it's likely a Creative-spawned tool, or the mod was added to a world -- ((world.Side.IsClient()) || (
                int remainingBindingDur = itemStack.GetToolbindingCurrentDurability();
                float chanceToDamage = itemStack.GetGripChanceToDamage();
                bool isBluntTool = itemslot.Itemstack.Collectible.HasBehavior<CollectibleBehaviorToolBlunt>();
                bool headBroke = false;

                //Sharpness always drops; which parts take durability damage depends on how sharp the tool still was.
                bool doDamageHead = false;
                bool doubleHeadDamage = false;
                bool doDamageHandle = false;
                bool doDamageBinding = false;

                //First, the most important question, what is the current sharpness percentage? Then lower the sharpness by amount.
                int currentSharpness = itemStack.GetToolCurrentSharpness();
                int maxSharpness = itemStack.GetToolMaxSharpness();
                float sharpnessPer = itemStack.GetToolSharpnessPercent();

                if (maxSharpness <= 1) { //If the Sharpness Max is 1, likely means something got marked improperly. I don't think it could be 1 otherwise?
                    sharpnessPer = 0f; //Set the percent to one as a placeholder to just avoid infinite sharpness.
                }
                if (currentSharpness > 0) {
                    currentSharpness -= amount;
                }
                else {
                    doubleHeadDamage = true;
                }

                if (!isBluntTool) {
                    itemStack.SetToolCurrentSharpness(currentSharpness);
                    itemStack.SetTotalHoneValue(0);
                }

                //Then based on the percentage, which parts do we actually damage?
                //Above 0.98, nothing on the tool takes durability damage as a little bonus
                if (sharpnessPer >= 0.98f) { //Since even if the tool isn't going to regularly take damage to all parts, we still want to damage the binding by 1 point just cause it's been 'used' once. It won't get damaged again anyway.
                    if (remainingBindingDur == itemStack.GetToolbindingMaxDurability()) {
                        remainingBindingDur -= 1;
                    }
                } else if (sharpnessPer >= 0.95f) {
                    doDamageBinding = true;
                } else {
                    doDamageBinding = true;
                    doDamageHandle = true;
                }

                if (sharpnessPer < 0.98f) {
                    doDamageHead = true;
                }

                //Handle damaging each part, the handle only if it should based on the chance to damage it
                if (doDamageHead && (doubleHeadDamage || (!isBluntTool && world.Rand.NextDouble() <= ToolsmithModSystem.Config.SharpWear) || world.Rand.NextDouble() <= ToolsmithModSystem.Config.BluntWear)) { //If this Tinkered Tool is also marked as a blunted tool, then apply the much much smaller chance to damage it. Damage the other parts though!
                    remainingHeadDur -= amount;
                    if (doubleHeadDamage) {
                        remainingHeadDur -= amount;
                    }
                }
                itemStack.SetToolheadCurrentDurability(remainingHeadDur);
                if (remainingHeadDur <= 0) {
                    headBroke = true;
                }

                if (doDamageHandle && chanceToDamage < 1.0f) {
                    var damageToTake = 0;
                    if (world.Rand.NextDouble() <= chanceToDamage) {
                        damageToTake++;
                    }

                    if (amount > 1) { //Only run this if it's actually needed, to actually 'roll' for damage each point of damage. Is there a better way to do this?
                        var count = 1;
                        while (count < amount) {
                            if (world.Rand.NextDouble() <= chanceToDamage) { //For each point of damage, roll for change to damage
                                damageToTake++;
                            }
                            count++;
                        }
                    }
                    remainingHandleDur -= damageToTake;
                } else if (doDamageHandle) {
                    remainingHandleDur -= amount;
                }
                itemStack.SetToolhandleCurrentDurability(remainingHandleDur);

                if (doDamageBinding) {
                    remainingBindingDur -= amount;
                }
                itemStack.SetToolbindingCurrentDurability(remainingBindingDur);

                if (remainingHeadDur > 0) {
                    DrawbackUtility.TryChanceForDrawback(world, byEntity, itemslot, sharpnessPer);
                }

                //Check each part and see if the health of any of them is <= 0, thus the tool broke, handle it
                //Any or all parts COULD hit 0 at the same time, technically. I'd love to see it though, but it needs to be possible!
                bool toolNeedsDestroying = false;
                bool anyPartBroke = (remainingBindingDur <= 0 || remainingHandleDur <= 0 || remainingHeadDur <= 0);

                //Logged before the branch rather than inside it, so a damage event that breaks nothing still shows up.
                //Vanilla's own durability write never runs for a tinkered tool - bhHandling is PreventDefault above -
                //so the vanilla durability logged here is only ever changed by Toolsmith itself.
                if (ToolsmithModSystem.Config.DebugMessages) {
                    ToolsmithModSystem.Logger.Debug("[BreakTrace] OnDamageItem on " + itemStack.Collectible.Code + ", amount " + amount + ", slot " + itemslot.GetType().Name + ", inventory " + (itemslot.Inventory == null ? "NULL (projectile or dummy)" : itemslot.Inventory.GetType().Name));
                    ToolsmithModSystem.Logger.Debug("[BreakTrace] post-damage head/handle/binding: " + remainingHeadDur + " / " + remainingHandleDur + " / " + remainingBindingDur + ", headBroke: " + headBroke + ", anyPartBroke: " + anyPartBroke);
                    ToolsmithModSystem.Logger.Debug("[BreakTrace] vanilla durability reads " + itemStack.Collectible.GetRemainingDurability(itemStack) + " (never decremented by vanilla here - PreventDefault is set)");
                }

                if (anyPartBroke) {
                    toolNeedsDestroying = TinkeringUtility.HandleBrokenTinkeredTool(world, byEntity, itemslot, remainingHeadDur, currentSharpness, remainingHandleDur, remainingBindingDur, headBroke, !headBroke);
                }

                itemslot.MarkDirty();
                //Exactly one of the two empties the slot, and the helper says which. Destroying a slot it already
                //emptied would refill it from the inventory a second time, handing back a whole tool alongside the
                //parts that were just returned.
                if (toolNeedsDestroying) {
                    itemStack.Collectible.DestroyItem(world, byEntity, itemslot);
                }
            } else if (!world.Side.IsServer()) {
                bhHandling = EnumHandling.PreventDefault; //This should prevent the clientside from running the vanilla damage calculations, but the serverside is still doing it's proper stuff above. Then the client will recieve the update, so there is no desync.
            }

            //Falling through both branches means this is the server and the item is on the ignore list, so vanilla
            //damage handling runs untouched.
        }

        public override int GetMaxDurability(ItemStack itemstack, int durability, ref EnumHandling bhHandling) {
            bhHandling = EnumHandling.PreventDefault;
            return TinkeringUtility.ScaleToHeadDurability(durability);
        }

        public override float GetMiningSpeed(ItemStack itemstack, BlockSelection blockSel, Block block, IPlayer forPlayer, ref EnumHandling bhHandling) {
            bhHandling = EnumHandling.Handled;
            return TinkeringUtility.TinkeredToolMiningSpeedMultiplier(itemstack);
        }

        //GetMiningSpeed is what the game asks when the tool actually swings; GetMiningSpeedModifier is what it asks
        //when building the tooltip. They are separate methods, so overriding only the first left the tooltip showing
        //vanilla's flat constant for every tool - the same numbers regardless of handle, grip or material, even
        //though mining really was faster. Both now come from one place so they cannot drift apart again.
        public override float GetMiningSpeedModifier(ItemStack itemstack, ref EnumHandling bhHandling) {
            bhHandling = EnumHandling.Handled;
            return GlobalConstants.ToolMiningSpeedModifier * TinkeringUtility.TinkeredToolMiningSpeedMultiplier(itemstack);
        }
    }
}
