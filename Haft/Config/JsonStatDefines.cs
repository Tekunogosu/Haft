using Newtonsoft.Json;

namespace Haft.Config {
    //Most stats here are required and expected unless otherwise stated!
    public class HaftStat : IHaftDefine {
        [JsonProperty]
        public string id = null;

        public string Id => id; //An ID to help access and find what it is - make sure this is the same as the Dictionary Key. It might help to keep an id associated with the stats.

        //What this stat block IS, as arbitrary labels, matched against a part's requiresTags. On a material this is
        //how "wood" and "metal" become gateable without a handle part existing per material. Same free-string rule as
        //HaftPart.providesTags - see the comment there.
        [JsonProperty]
        public string[] providesTags = new string[0];
    }

    public class HandleStatDefines : HaftStat { //In an effort to keep things similarly vanilla for durability values, the baseHPfactor is a multiplier on the base durability of the tool-to-be-crafted
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
    public class MaterialStatDefines : HaftStat {
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
        //0.0 is the oak baseline. Woods sit near it in a narrow band (kapok +0.07 to ebony -0.04) because a wooden
        //handle's speed comes mostly from its tier; metals sit far below it. For a bow this field carries more weight
        //than it does for a handle: a lighter limb is genuinely easier to draw and hold at full draw.
        [JsonProperty]
        public float speedBonus = 0.0f;

        //Stiffness - how far the material springs back rather than taking a permanent set. Oak is the 1.0 baseline,
        //derived from real modulus of elasticity (oak ~12.3 GPa) the same way densityFactor is derived from Janka.
        //
        //This is deliberately NOT densityFactor. Janka measures resistance to denting; a bow limb needs elasticity,
        //and the two order the woods almost oppositely - ebony is the hardest wood in the table and a terrible bow.
        //The classic bow woods are not the stiffest, they have the best ratio of springback to stiffness, so bow
        //behaviour reads BOTH fields rather than either alone.
        //
        //Unset (-1.0) means the material has no meaningful elastic behaviour and cannot serve as a limb.
        [JsonProperty]
        public float flexibility = -1.0f;

        //How quickly a limb of this material comes to full draw, as a multiplier on the time a shot needs. Oak is
        //1.00; below 1.00 is quicker, above is slower.
        //
        //Separate from speedBonus rather than derived from it, because they are not the same physical fact. A
        //handle's speedBonus is how fast the tool swings, governed by the mass on the end of an arm; a limb's draw
        //speed is governed by limb mass and the stiffness curve the archer pulls against. Purpleheart being slow to
        //draw is not the same claim as a purpleheart handle being slow to swing, and one cannot be read off the
        //other. Reusing speedBonus also inherited a band tuned for handles, +0.07 to -0.04, which is +-7% of the
        //0.65s draw gate - about 45ms across every wood in the game, below what anyone can feel. This axis is what
        //pays for high draw weight, so it needs a range that can actually pay.
        //
        //Derived for the shipped materials as ((densityFactor^1.4) * (flexibility^0.4))^0.7, oak landing on 1.00 by
        //construction. Density carries the larger exponent because draw effort is dominated by the mass being moved;
        //stiffness matters to a limb, but less than mass for how long a bow takes to reach full draw. An earlier
        //version weighted the two equally as sqrt(density * flexibility), which left ebony and purpleheart within
        //0.01 of each other despite a 0.45 difference in density, and put both close enough to oak that the heaviest
        //woods cost almost nothing to shoot.
        //
        //The expression is recorded here rather than computed at load: these are config values a pack author is free
        //to set by hand, and a material that wants a draw time the formula does not produce should be able to say so.
        //Regenerating the shipped set means applying the expression above to every entry, not editing one.
        //
        //Unset (-1.0) means the material has no bow draw data, same rule as flexibility above: a material nobody
        //has given a bow value to must not silently draw as fast as oak.
        [JsonProperty]
        public float drawSpeedBonus = -1.0f;
    }

    public class GripStatDefines : HaftStat {
        [JsonProperty]
        public string texturePath = "plain"; //The default here is effectively no path.

        [JsonProperty]
        public string langTag = ""; //A tag to set for localization purposes that describes the grip on the tool IE: "grip-cloth" for cloth

        [JsonProperty]
        public float speedBonus = -1.0f; //The best speed bonuses come from the grip of the tool. If you can hold it better, you can use it faster...

        [JsonProperty]
        public float chanceToDamage = -1.0f; //And more efficiently too. Gives the handle a chance to ignore damage!
    }

    public class TreatmentStatDefines : HaftStat {
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

        //What the treatment contributes to a bow limb shrugging off a shot's wear, fed into the limb's own refund
        //roll rather than into chanceToDamageReduction above.
        //
        //A bow gets its own field because a limb wears differently from a haft. A handle fails by being battered
        //across its length, which is what chanceToDamageReduction models; a limb fails by being cycled to full draw
        //and back thousands of times and slowly staying bent. A finish that helps one does not automatically help
        //the other by the same amount, so the two are tuned apart instead of one being read for both.
        //
        //Zero is a real value here, not an unset sentinel: it means this treatment does nothing for a limb, which is
        //the correct reading for most of them.
        [JsonProperty]
        public float limbRefundBonus = 0.0f;
}

    public class BindingStatDefines : HaftStat {
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
