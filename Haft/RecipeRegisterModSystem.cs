using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Haft.Client;
using Haft.Config;
using Haft.ToolTinkering.Behaviors;
using Haft.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Haft {
    public class RecipeRegisterModSystem : ModSystem {

        //A Dictionary to give it the Tool Head's Code.ToString to retreive the Tool CollectibleObject it should create
        public static Dictionary<string, CollectibleObject> TinkerToolGridRecipes;
        //A Dictionary to supply it with a tool head when looking to reforge it, getting the recipe back that produces the item to generate the WorkPiece similar to Smithing Plus
        public static Dictionary<string, SmithingRecipe> ToolHeadSmithingRecipes;
        //Textures are grabbed from the ToolHeads once found
        public static Dictionary<string, ToolHeadTextureData> ToolHeadTexturesCache;

        //This is instantiated and populated in the HaftModSystem, then used here to parse through the recipes once they have loaded, nulled afterwards for space.
        public static List<CollectibleObject> TinkerableToolsList;

        public static List<CollectibleObject> HandleList; //Now populated to generate Grid Recipes with the tool heads, handles, and bindings. Is cleared afterwards to free space, so do not expect it to remain populated.
        public static List<CollectibleObject> BindingList; //^^^
        public static List<CollectibleObject> GripList; //^^^
        public static List<CollectibleObject> BowList; //The parted bows, which take a grip the way a handle does.
        public static List<CollectibleObject> TreatmentList; //^^^
        public static List<CollectibleObject> LiquidContainers; //^^^

        public override bool ShouldLoad(EnumAppSide forSide) {
            return forSide == EnumAppSide.Server;
        }

        public override double ExecuteOrder() {
            return 1;
        }

        public override void StartPre(ICoreAPI api) {
            TinkerToolGridRecipes = new Dictionary<string, CollectibleObject>();
            ToolHeadSmithingRecipes = new Dictionary<string, SmithingRecipe>();
            ToolHeadTexturesCache = new Dictionary<string, ToolHeadTextureData>();
        }

        public override void AssetsFinalize(ICoreAPI api) {
            base.AssetsFinalize(api);

            var headITags = api.CollectibleTagRegistry.CreateTagSet(["haft-part", "haft-head"]);

            if (HaftModSystem.Config.PrintAllParsedToolsAndParts) {
                HaftModSystem.Logger.Debug("Tool Heads:");
            }
            //Every grid recipe is scanned once at load: a recipe whose output is a tinkerable tool and whose
            //ingredients include a configured tool head identifies that head, which is then given its behavior and
            //recorded against the tool it produces.
            List<GridRecipe> toolRecipes = new List<GridRecipe>();
            foreach (var recipe in api.World.GridRecipes) { //Check each recipe...
                foreach (var tool in TinkerableToolsList.Where(t => recipe.Output.Code.Equals(t.Code))) { //Where the output code matches anything on the Tinkered Tool List (from the configs)...
                    foreach (var ingredient in recipe.Ingredients.Where(i => (i.Value != null) && (i.Value.Code != null) && (i.Value.ResolvedItemStack != null) && (ConfigUtility.IsToolHead(i.Value.Code.ToString())))) { //And the recipe in question has a Tool Head item that is on the Tool Head Config List
                        if (!ingredient.Value.ResolvedItemStack.Collectible.HasBehavior<CollectibleBehaviorToolHead>()) {
                            ingredient.Value.ResolvedItemStack.Collectible.AddBehavior<CollectibleBehaviorToolHead>(); //Therefore it is a Tool Head! Give it the behavior.
                        }

                        if (ConfigUtility.IsBluntTool(tool.Code)) { //If it is also a blunt tool, add the 'nodamage' Behavior as a tag to the Head as well
                            if (!ingredient.Value.ResolvedItemStack.Collectible.HasBehavior<CollectibleBehaviorToolBlunt>()) {
                                ingredient.Value.ResolvedItemStack.Collectible.AddBehavior<CollectibleBehaviorToolBlunt>();
                            }
                        }

                        if (ingredient.Value.ResolvedItemStack.Item != null && !headITags.IsFullyContainedIn(in ingredient.Value.ResolvedItemStack.Item.Tags)) {
                            var tagArray = ingredient.Value.ResolvedItemStack.Item.Tags;
                            ingredient.Value.ResolvedItemStack.Item.Tags = api.CollectibleTagRegistry.CreateMergedTagSet(tagArray, headITags);
                        }

                        if (HaftModSystem.Config.EnableGridRecipesForToolCrafting) {
                            toolRecipes.AddRange(GenerateToolGridRecipes(api, ingredient.Value, tool));
                        }

                        var gridRecipeTag = ingredient.Value.Code.ToString();
                        foreach (var otherIngredients in recipe.Ingredients.Where(o => (o.Value != null) && (o.Value.Code != null) && (o.Value.Code.Path == "bone"))) { //Is this one of the bone + head recipes?
                            if (otherIngredients.Value.Code == HaftConstants.BoneHandleCode) {
                                gridRecipeTag += "-bone";
                                break;
                            }
                        }

                        if (!ToolHeadTexturesCache.ContainsKey(ingredient.Value.Code)) {
                            if (ingredient.Value.Type == EnumItemClass.Item) {
                                Item item = ingredient.Value.ResolvedItemStack.Item;
                                if (item.Textures != null) {
                                    ToolHeadTextureData textures = new ToolHeadTextureData();
                                    foreach (var tex in item.Textures) {
                                        textures.Tags.Add(tex.Key);
                                        textures.Paths.Add(tex.Value.Base);
                                    }
                                    ToolHeadTexturesCache.Add(item.Code, textures);
                                }
                            } else {
                                Block block = ingredient.Value.ResolvedItemStack.Block;
                                if (block.Textures != null) {
                                    ToolHeadTextureData textures = new ToolHeadTextureData();
                                    foreach (var tex in block.Textures) {
                                        textures.Tags.Add(tex.Key);
                                        textures.Paths.Add(tex.Value.Base);
                                    }
                                    ToolHeadTexturesCache.Add(block.Code, textures);
                                }
                            }
                        }

                        if (!TinkerToolGridRecipes.ContainsKey(gridRecipeTag)) {
                            TinkerToolGridRecipes.Add(gridRecipeTag, tool);
                            if (HaftModSystem.Config.PrintAllParsedToolsAndParts) {
                                HaftModSystem.Logger.Debug(ingredient.Value.Code.ToString());
                            }
                        }
                    }
                }
            }

            var handleRecipes = GenerateHandleRecipes(api);
            var adhesiveGripRecipes = GenerateAdhesiveGripRecipes(api);
            var bowGripRecipes = GenerateBowGripRecipes(api);

            if (toolRecipes != null && toolRecipes.Count > 0) {
                api.World.GridRecipes.AddRange(toolRecipes);
            }
            if (handleRecipes != null && handleRecipes.Count > 0) {
                api.World.GridRecipes.AddRange(handleRecipes);
            }
            if (adhesiveGripRecipes != null && adhesiveGripRecipes.Count > 0) {
                api.World.GridRecipes.AddRange(adhesiveGripRecipes);
            }
            if (bowGripRecipes != null && bowGripRecipes.Count > 0) {
                api.World.GridRecipes.AddRange(bowGripRecipes);
            }

            //Make sure to clean up the five lists that were used in all this here! Would be nice not to leave that overhead information when it likely won't be needed after this point.
            TinkerableToolsList = null;
            HandleList = null;
            BindingList = null;
            GripList = null;
            BowList = null;
            TreatmentList = null;
            LiquidContainers = null;

            toolRecipes = null;
            handleRecipes = null;
        }

        private List<GridRecipe> GenerateToolGridRecipes(ICoreAPI api, CraftingRecipeIngredient head, CollectibleObject tool) {
            var list = new List<GridRecipe>();
            TreeAttribute applyQuenchable = new TreeAttribute();
            applyQuenchable.SetBool("applyquenchablebuffs", true);

            foreach (var handle in HandleList) {
                foreach (var binding in BindingList) {
                    var recipe = new GridRecipe {
                        IngredientPattern = "hb,r_",
                        Width = 2,
                        Height = 2,
                        Ingredients = new Dictionary<string, CraftingRecipeIngredient> {
                            ["h"] = head.Clone(),
                            ["b"] = new CraftingRecipeIngredient { Type = binding.ItemClass, Code = binding.Code },
                            ["r"] = new CraftingRecipeIngredient { Type = handle.ItemClass, Code = handle.Code }
                        },
                        RecipeGroup = 2,
                        ShowInCreatedBy = false,
                        Name = "Craft a " + tool.Code + " from a " + handle.Code + " and " + binding.Code + ".",
                        Output = new CraftingRecipeIngredient { Type = tool.ItemClass, Code = tool.Code, RecipeAttributes = new JsonObject(JToken.Parse(applyQuenchable.ToJsonToken())) }
                    };
                    recipe.Resolve(api.World, "Generated Haft recipe for " + head.Code.ToString());
                    list.Add(recipe);
                }
            }

            if (list.Count > 0) {
                return list;
            } else {
                return null;
            }
        }

        //Backs a grip with an adhesive, so it will stick to a handle too smooth to grab on its own. The result is the
        //SAME grip item carrying an adhesive attribute, not a separate treated variant: there are around 130 grip part
        //defines resolving to only five stat blocks, so a treated twin per grip would mean 130 new defines and 130 new
        //recipes, and would oblige every future grip to ship one. An attribute is inherited by every grip that exists
        //or is ever added, for one recipe shape.
        //
        //The adhesives are read from the binding parts rather than a list of their own, since those already define
        //which glues exist and how much of each is used, and a mod adding a glue there gets this for free.
        private List<GridRecipe> GenerateAdhesiveGripRecipes(ICoreAPI api) {
            var list = new List<GridRecipe>();

            if (GripList == null || BindingList == null || LiquidContainers.Count == 0) {
                return null;
            }

            foreach (var binding in BindingList) {
                var bindingPart = HaftModSystem.Stats.BindingParts.TryGetValue(binding.Code.Path);
                if (bindingPart == null || !bindingPart.isLiquid) {
                    continue;
                }

                var bindingStats = HaftModSystem.Stats.BindingStats.TryGetValue(bindingPart.bindingStatTag);
                if (bindingStats == null || bindingStats.id != HaftConstants.AdhesiveBindingStatTag) {
                    continue;
                }

                ITreeAttribute liquidProps = new TreeAttribute();
                var liqProps = liquidProps.GetOrAddTreeAttribute("liquidContainerProps");
                var reqCont = liqProps.GetOrAddTreeAttribute("requiresContent");
                reqCont.SetString("type", "item");
                reqCont.SetString("code", binding.Code);
                liqProps.SetFloat("requiresLitres", bindingPart.litersUsed);

                foreach (var gripMat in GripList) {
                    foreach (var container in LiquidContainers) {
                        var recipe = new GridRecipe {
                            IngredientPattern = "ga",
                            Width = 2,
                            Height = 1,
                            Ingredients = new Dictionary<string, CraftingRecipeIngredient> {
                                ["g"] = new CraftingRecipeIngredient { Type = gripMat.ItemClass, Code = gripMat.Code },
                                ["a"] = new CraftingRecipeIngredient { Type = container.ItemClass, Code = container.Code }
                            },
                            Attributes = new JsonObject(JToken.Parse(liquidProps.ToJsonToken())),
                            RecipeGroup = 4,
                            ShowInCreatedBy = true,
                            Shapeless = true,
                            Name = "Back " + gripMat.Code + " with " + binding.Code + " so it will hold on a smooth handle.",
                            Output = new CraftingRecipeIngredient { Type = gripMat.ItemClass, Code = gripMat.Code }
                        };

                        recipe.Resolve(api.World, "Generating Haft Adhesive-Grip Recipe for " + gripMat.Code + " backed with " + binding.Code);
                        list.Add(recipe);
                    }
                }
            }

            return list.Count > 0 ? list : null;
        }

        //Wrapping a bow's riser, generated exactly as a handle's grip recipes are - same shapeless two-slot shape,
        //same recipe group, same in-place upgrade producing the bow it was given. A grip is a grip, so this reuses
        //GripList whole rather than introducing a bow-only set: a grip added for tools is craftable onto a bow the
        //moment it is defined.
        //
        //The tier decides whether a bow accepts one at all. A crude bow is sticks bound with cordage and has no
        //riser to wrap, which is the same answer canHaveGrip gives for the handles that cannot take one.
        private List<GridRecipe> GenerateBowGripRecipes(ICoreAPI api) {
            var list = new List<GridRecipe>();

            if (BowList == null || GripList == null) {
                return null;
            }

            foreach (var bow in BowList) {
                var tierKey = bow.Variant?["type"];
                if (tierKey == null || !HaftConstants.BowStatKeyByVariant.TryGetValue(tierKey, out var statKey)) {
                    continue;
                }

                var bowStats = HaftModSystem.Stats.BowStats.TryGetValue(statKey);
                if (bowStats == null || !bowStats.canHaveGrip) {
                    continue;
                }

                foreach (var gripMat in GripList) {
                    var recipe = new GridRecipe {
                        IngredientPattern = "bg",
                        Width = 2,
                        Height = 1,
                        Ingredients = new Dictionary<string, CraftingRecipeIngredient> {
                            ["b"] = new CraftingRecipeIngredient { Type = bow.ItemClass, Code = bow.Code },
                            ["g"] = new CraftingRecipeIngredient { Type = gripMat.ItemClass, Code = gripMat.Code }
                        },
                        RecipeGroup = 2,
                        ShowInCreatedBy = true,
                        Shapeless = true,
                        Name = "Add " + gripMat.Code + " as a bow grip.",
                        Output = new CraftingRecipeIngredient { Type = bow.ItemClass, Code = bow.Code }
                    };
                    recipe.Resolve(api.World, "Generating Haft Bow-grip Recipe for " + bow.Code + " with a grip made of " + gripMat.Code);
                    list.Add(recipe);
                }
            }

            return list.Count > 0 ? list : null;
        }

        private List<GridRecipe> GenerateHandleRecipes(ICoreAPI api) {
            var list = new List<GridRecipe>();
            foreach (var handle in HandleList) { //For every handle base that was found...
                HandlePartDefines handlesStats = HaftModSystem.Stats.BaseHandleParts.TryGetValue(handle.Code.Path); //Grab the stat pair that should be registered in the configs here.
                if (handlesStats != null) { //Just in case, ensure it was found!
                    //Check the stats of the handle found, see what recipes are required to make for this one. Grip? Treatment? Both or neither?
                    //If Grip is allowed on this handle, generate a recipe with each of the Grip ingredients and add to list. Leave the actual assigning of attributes to the Handle Behavior's OnCreatedByCrafting call
                    if (handlesStats.canHaveGrip) {
                        foreach (var gripMat in GripList) {
                            var recipe = new GridRecipe {
                                IngredientPattern = "hg",
                                Width = 2,
                                Height = 1,
                                Ingredients = new Dictionary<string, CraftingRecipeIngredient> {
                                    ["h"] = new CraftingRecipeIngredient { Type = handle.ItemClass, Code = handle.Code },
                                    ["g"] = new CraftingRecipeIngredient { Type = gripMat.ItemClass, Code = gripMat.Code }
                                },
                                RecipeGroup = 2,
                                ShowInCreatedBy = true,
                                Shapeless = true,
                                Name = "Add " + gripMat.Code + " as a handle grip.",
                                Output = new CraftingRecipeIngredient { Type = handle.ItemClass, Code = handle.Code }
                            };
                            recipe.Resolve(api.World, "Generating Haft Handle-grip Recipe for " + handle.Code + " with a grip made of " + gripMat.Code);
                            list.Add(recipe);
                        }
                    }

                    //Same for Treatments here! Add to the list afterwards!
                    if (handlesStats.canBeTreated) {
                        foreach (var treatmentMat in TreatmentList) {
                            var treatmentStats = HaftModSystem.Stats.TreatmentParts.Get(treatmentMat.Code.Path);
                            if (treatmentStats != null && !treatmentStats.isLiquid) {
                                var recipe = new GridRecipe {
                                    IngredientPattern = "ht",
                                    Width = 2,
                                    Height = 1,
                                    Ingredients = new Dictionary<string, CraftingRecipeIngredient> {
                                        ["h"] = new CraftingRecipeIngredient { Type = handle.ItemClass, Code = handle.Code },
                                        ["t"] = new CraftingRecipeIngredient { Type = treatmentMat.ItemClass, Code = treatmentMat.Code }
                                    },
                                    RecipeGroup = 3,
                                    ShowInCreatedBy = true,
                                    Shapeless = true,
                                    Name = "Add " + treatmentMat.Code + " as a handle treatment.",
                                    Output = new CraftingRecipeIngredient { Type = handle.ItemClass, Code = handle.Code }
                                };
                                recipe.Resolve(api.World, "Generating Haft Handle-Treatment Recipe for " + handle.Code + " treated with " + treatmentMat.Code);
                                list.Add(recipe);
                            } else if (treatmentStats != null && LiquidContainers.Count > 0) {
                                ITreeAttribute liquidProps = new TreeAttribute();
                                var liqProps = liquidProps.GetOrAddTreeAttribute("liquidContainerProps");
                                var reqCont = liqProps.GetOrAddTreeAttribute("requiresContent");
                                reqCont.SetString("type", "item");
                                reqCont.SetString("code", treatmentMat.Code);
                                liqProps.SetFloat("requiresLitres", treatmentStats.litersUsed);
                                
                                foreach (var container in LiquidContainers) {
                                    var bucketRecipe = new GridRecipe {
                                        IngredientPattern = "ht",
                                        Width = 2,
                                        Height = 1,
                                        Ingredients = new Dictionary<string, CraftingRecipeIngredient> {
                                            ["h"] = new CraftingRecipeIngredient { Type = handle.ItemClass, Code = handle.Code },
                                            ["t"] = new CraftingRecipeIngredient { Type = container.ItemClass, Code = container.Code }
                                        },
                                        Attributes = new JsonObject(JToken.Parse(liquidProps.ToJsonToken())),//new JsonObject(JToken.Parse("{liquidContainerProps: {requiresContent: {type: \"item\", code: \"" + treatmentMat.Code + "\" }, requiresLitres: " + treatmentStats.litersUsed + "}}")),
                                        RecipeGroup = 3,
                                        ShowInCreatedBy = true,
                                        Shapeless = true,
                                        Name = "Add " + treatmentMat.Code + " as a handle treatment.",
                                        Output = new CraftingRecipeIngredient { Type = handle.ItemClass, Code = handle.Code }
                                    };

                                    bucketRecipe.Resolve(api.World, "Generating Haft Handle-LiquidTreatment Recipe for " + handle.Code + " treated with " + treatmentMat.Code);
                                    list.Add(bucketRecipe);
                                }
                            }
                        }
                    }
                }
            }

            if (list.Count > 0) {
                return list;
            } else {
                return null;
            }
        }

        public override void Dispose() {
            TinkerToolGridRecipes = null;
            ToolHeadSmithingRecipes = null;
            TinkerableToolsList = null;
            ToolHeadTexturesCache = null;
            base.Dispose();
        }
    }
}
