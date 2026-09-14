using Newtonsoft.Json;

namespace Toolsmith.Config {
    //Most stats here are required and expected unless otherwise stated!
    public class ToolsmithStat : IToolsmithDefine {
        [JsonProperty]
        public string id = null;

        public string Id => id; //An ID to help access and find what it is - make sure this is the same as the Dictionary Key. It might help to keep an id associated with the stats.

        //What this stat block IS, as arbitrary labels, matched against a part's requiresTags. On a material this is
        //how "wood" and "metal" become gateable without a handle part existing per material. Same free-string rule as
        //ToolsmithPart.providesTags - see the comment there.
        [JsonProperty]
        public string[] providesTags = new string[0];
    }

    public class HandleStatDefines : ToolsmithStat { //In an effort to keep things similarly vanilla for durability values, the baseHPfactor is a multiplier on the base durability of the tool-to-be-crafted
        [JsonProperty]
        public float baseHPfactor = -1.0f; //It's the main part of Handles and Bindings.
        
        [JsonProperty]
        public float selfHPBonus = -1.0f; //For more advanced handles, provides an additional multiplier for the handle's health as a bonus ontop

        [JsonProperty]
        public float bindingHPBonus = -1.0f; //Advanced handles can provide a small bonus to the Binding's HP

        [JsonProperty]
        public float speedBonus = -1.0f; //Advanced handles can make it easier to use the tool as well!
    }

    //The wood a handle was shaped from. Kept as its own stat type rather than folded into HandleStatDefines because
    //the two vary independently - an oak handle and an oak carpented handle share a wood but not a tier, and a
    //crude handle has a tier but no wood at all (it is made from firewood, which carries no wood variant).
    public class MaterialStatDefines : ToolsmithStat {
        [JsonProperty]
        public float densityFactor = -1.0f; //Scales the handle's base durability. Oak is the 1.0 baseline: woods are derived from real Janka hardness, metals sit above the whole wood range.

        //The name densityFactor carries in an older config. Compat mods ship hardnessFactor, so it is still read and
        //folded into densityFactor at verification time. Only ever written by Json, never read by mod code.
        [JsonProperty]
        public float hardnessFactor = -1.0f;

        [JsonProperty]
        public float nailBindingBonus = -1.0f; //A denser material holds a nail better. Only ever applied to metal bindings - a rope wrap does not care what it is tightened around.

        //How the material itself affects swing speed, added to whatever the handle tier and grip contribute.
        //Density and speed are not the same axis: metal is dense and slow to swing, so metals carry a penalty here
        //while being far more durable. Meteoric iron is the exception the field exists for - it is the one metal
        //light enough to swing well, which is what makes it worth seeking out over plain iron.
        //0.0 is neutral, which is what every wood has: a wooden handle's speed comes from its tier, not its species.
        [JsonProperty]
        public float speedBonus = 0.0f;
    }

    public class GripStatDefines : ToolsmithStat {
        [JsonProperty]
        public string texturePath = "plain"; //The default here is effectively no path.

        [JsonProperty]
        public string langTag = ""; //A tag to set for localization purposes that describes the grip on the tool IE: "grip-cloth" for cloth

        [JsonProperty]
        public float speedBonus = -1.0f; //The best speed bonuses come from the grip of the tool. If you can hold it better, you can use it faster...

        [JsonProperty]
        public float chanceToDamage = -1.0f; //And more efficiently too. Gives the handle a chance to ignore damage!
    }

    public class TreatmentStatDefines : ToolsmithStat {
        [JsonProperty]
        public string langTag = ""; //A tag to set for localization purposes that describes the treatment on the tool IE: "treatment-wax" for wax

        [JsonProperty]
        public float handleHPbonus = -1.0f; //Treating the handle makes it last longer
    
        //The share of handle damage the treatment shrugs off entirely, multiplied into the same chanceToDamage roll
        //a grip feeds. 0.0 means no reduction.
        //
        //This is the axis that separates the two kinds of finish, and the split is deliberate. A wood treatment soaks
        //into the material, so it both strengthens the handle and sheds wear - it carries a handleHPbonus AND a
        //reduction. Bluing does not: it converts the surface to a layer of magnetite a few microns deep, which
        //passivates the steel against corrosion without changing its strength at all. A blued part is exactly as
        //strong as an unblued one, so bluing carries reduction ONLY and leaves handleHPbonus at zero.
        //Structural gains belong to tempering, which is a real change to the metal rather than to its surface.
        [JsonProperty]
        public float chanceToDamageReduction = 0.0f;
}

    public class BindingStatDefines : ToolsmithStat {
        [JsonProperty]
        public string texturePath = "plain";

        [JsonProperty]
        public string langTag = "";

        [JsonProperty]
        public float baseHPfactor = -1.0f;

        [JsonProperty]
        public float selfHPBonus = -1.0f;

        [JsonProperty]
        public float handleHPBonus = -1.0f;

        [JsonProperty]
        public float recoveryPercent = -1.0f; //If the HP is below this percent, then the binding is ruined if another part breaks

        [JsonProperty]
        public bool isMetal = false; //If true and the bindings break, try and return some bits

        [JsonProperty]
        public string metalType = null; //For ease of returning the bits, the material/metal variant of bits to return! Only needs to be set if IsMetal is true!
    }
}
