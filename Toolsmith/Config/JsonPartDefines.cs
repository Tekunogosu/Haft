using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toolsmith.Config {

    public class ToolsmithPart {
        [JsonProperty]
        public bool enabled = true;

        [JsonProperty]
        public string id = null;

        //Arbitrary labels describing what this part is, and what it will accept. Two lists rather than one field per
        //rule, so a new restriction needs no new schema: tag the parts, state the requirement, done.
        //
        //providesTags says what this part IS - a treatment tagging itself "wood-treatment", a grip tagging itself
        //"adhesive-backed". requiresTags says what a part it is applied TO must provide. A part with no requiresTags
        //restricts nothing, which is why every existing part and every compat mod keeps working untouched.
        //
        //Matching is exact, case-insensitive, and ALL of requiresTags must be present. Tags are free strings on
        //purpose: a content mod can invent its own and gate on it without this mod knowing the name.
        [JsonProperty]
        public string[] providesTags = new string[0];

        [JsonProperty]
        public string[] requiresTags = new string[0];
    }

    public class HandlePartDefines : ToolsmithPart { //The ID is mandatory for each of these, it should always be the code of the item as written in the ItemTypes defines. It becomes the Dictionary entry and the search parameter to get that part.
        [JsonProperty]
        public string handleStatTag = null; //The associated stat block id for this handle. Also must be set to something!

        [JsonProperty]
        public bool canHaveGrip = false; //Can this handle have a grip or not? Default to no, but not mandatory.

        [JsonProperty]
        public string handleShapePath = ""; //The part shape path for Multi-Part Rendering purposes. Not required, but also doesn't hurt to set it - if it doesn't find the shape, it will fall back to the default tool's shape.

        [JsonProperty]
        public bool canBeTreated = false; //Can this handle be treated? Generally left for more 'proper' handles, but can be valid for any. Default to no.

        [JsonProperty]
        public float dryingTimeMult = 1.0f; //If this handle can be treated, this is a multiplier on all treatments drying times when applied to this handle.
    }

    public class GripPartDefines : ToolsmithPart {
        [JsonProperty]
        public string gripStatTag = null;

        [JsonProperty]
        public string gripShapePath = "";

        [JsonProperty]
        public string gripTextureOverride = ""; //If the Grip has a Shape Path for Multi-Part rendering, setting this to a path to a texture will tell the Multi-Part system to use this texture in place of whatever the Stat Block has for the 'base' texture. IE the colored Leathers is a good use case example!
    }

    public class TreatmentPartDefines : ToolsmithPart {
        [JsonProperty]
        public string treatmentStatTag = null;

        [JsonProperty]
        public int dryingHours = 12; //Base number of hours it takes to dry a handle, multiplied by the handle's drying time multiplier when applied.

        [JsonProperty]
        public bool isLiquid = false; //Is this treatment a liquid in a bowl/bucket or a solid item? Set it to true for proper liquid handling for recipes and such.

        [JsonProperty]
        public float litersUsed = 0.0f; //If the above is true, how many Liters are consumed on application?
    }

    public class BindingPartDefines : ToolsmithPart {
        [JsonProperty]
        public string bindingStatTag = null;

        [JsonProperty]
        public bool isLiquid = false;

        [JsonProperty]
        public float litersUsed = 0.0f;

        [JsonProperty]
        public string bindingShapePath = "";

        [JsonProperty]
        public string bindingTextureOverride = "";
    }
}
