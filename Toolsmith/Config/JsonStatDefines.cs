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
    public class WoodStatDefines : ToolsmithStat {
        [JsonProperty]
        public float hardnessFactor = -1.0f; //Scales the handle's base durability. Derived from real Janka hardness, oak as the 1.0 baseline.

        [JsonProperty]
        public float nailBindingBonus = -1.0f; //Harder wood holds a nail better. Only ever applied to metal bindings - a rope wrap does not care what it is tightened around.
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
