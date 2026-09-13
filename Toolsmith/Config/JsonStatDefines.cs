using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toolsmith.Config {
    //Most stats here are required and expected unless otherwise stated!
    public class ToolsmithStat {
        [JsonProperty]
        public string id = null; //An ID to help access and find what it is - make sure this is the same as the Dictionary Key. It might help to keep an id associated with the stats.

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
    
        //A treated surface resists wear as well as lasting longer: this is the share of handle damage the treatment
        //shrugs off entirely, multiplied into the same chanceToDamage roll a grip feeds. Scales with the effort the
        //treatment takes, so a wipe of grease saves a little and a proper blued finish saves the most.
        //0.0 means no reduction, which is what every wood treatment has until someone decides otherwise.
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
