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

    //How well a bow was built, as distinct from what it was built from. This is the limb's counterpart to
    //HandleStatDefines: a handle's durability is base * tier * material, and a bow's draw weight and limb life are
    //the same shape, so the two axes are kept in separate tables for the same reason. A bowyer working a dried stave
    //and a traveller lashing sticks together can both be holding oak, and the bow is not the same bow.
    //
    //The tier is read from the item's own code rather than recorded on the stack: unlike a handle's wood, which is
    //decided at sawing and has to survive the trip, a bowparted-long is always a stave bow and cannot become
    //anything else. There is nothing to save because there is nothing that can change.
    public class BowStatDefines : HaftStat {
        //Scales the energy the limb stores, multiplied against the material's own draw weight. The stave tier is the
        //1.0 baseline rather than the weakest, matching how the handle table puts its reference at the tier the mod
        //is actually about: a stave bow is what the wood axis was built for, and the lesser tiers are fractions of it.
        [JsonProperty]
        public float drawWeightFactor = -1.0f;

        //Scales how many shots the bow has in it, multiplied against the material's springback. Separate from
        //drawWeightFactor because build quality moves the two independently - a roughly-tillered limb that is not
        //bending evenly both stores less and fails sooner, but not by the same amount.
        [JsonProperty]
        public float limbDurabilityFactor = -1.0f;

        //Whether the bow records the wood it was built from. False means the tier has no wood axis at all: the bow
        //takes the default material's numbers and never names a species, which is what the crude handle already does
        //by having no wood variant to record. It is a property of the tier rather than of any one bow because it is
        //a statement about how the thing is made - sticks lashed together are not a species of wood.
        [JsonProperty]
        public bool hasWoodAxis = true;

        //Whether a bow of this tier can be given a grip, mirroring HandlePartDefines.canHaveGrip. The gate is the
        //tier rather than the individual bow for the same reason the wood axis is: it is a statement about how the
        //thing is built. A crude bow is sticks bound with cordage and has no riser to wrap.
        [JsonProperty]
        public bool canHaveGrip = true;
    }

    //The wood a handle was shaped from. Kept as its own stat type rather than folded into HandleStatDefines because
    //the two vary independently - an oak handle and an oak carpented handle share a wood but not a tier, and a
    //crude handle has a tier but no wood at all (it is made from firewood, which carries no wood variant).
    public class MaterialStatDefines : HaftStat {
        //Resistance to denting and wear, which is what decides how long a handle lasts. Oak is the 1.0 baseline:
        //woods are derived from real Janka hardness, metals sit above the whole wood range.
        //
        //This is deliberately NOT density. The two order the materials almost oppositely at the metal end - gold and
        //lead are among the densest materials here and among the softest, which is why a gold handle wears out fast
        //while a steel one does not. A field named for mass but holding hardness invited exactly that confusion.
        [JsonProperty]
        public float hardness = -1.0f;

        //Mass per unit volume, as a multiplier with oak at 1.0, derived from real density as (g/cm3 / 0.75)^0.35.
        //
        //The exponent compresses a range that is otherwise unusable: gold is 25x oak by raw density, and feeding
        //that into a draw-weight product tuned for values near 1.0 produces a bow no archer could bend. One curve
        //covers wood and metal rather than a per-band constant, so a material added later needs no special case.
        //
        //Used for bow draw weight, where what matters is the mass the limb throws, not how well it resists denting.
        //Unset (-1.0) means the material has no bow mass data, the same rule flexibility uses below.
        [JsonProperty]
        public float density = -1.0f;

        [JsonProperty]
        public float nailBindingBonus = -1.0f; //A harder material holds a nail better. Only ever applied to metal bindings - a rope wrap does not care what it is tightened around.

        //How the material itself affects swing speed, added to whatever the handle tier and grip contribute.
        //Mass and speed are not the same axis: metal is dense and slow to swing, so metals carry a penalty here
        //while being far more durable. Meteoric iron is the exception the field exists for - it is the one metal
        //light enough to swing well, which is what makes it worth seeking out over plain iron.
        //0.0 is the oak baseline. Woods sit near it in a narrow band (kapok +0.07 to ebony -0.04) because a wooden
        //handle's speed comes mostly from its tier; metals sit far below it. For a bow this field carries more weight
        //than it does for a handle: a lighter limb is genuinely easier to draw and hold at full draw.
        [JsonProperty]
        public float speedBonus = 0.0f;

        //Stiffness - how far the material springs back rather than taking a permanent set. Oak is the 1.0 baseline,
        //derived from real modulus of elasticity (oak ~12.3 GPa) the same way hardness is derived from Janka.
        //
        //This is deliberately NOT hardness. Janka measures resistance to denting; a bow limb needs elasticity,
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
        //Derived for the shipped materials as ((hardness^1.4) * (flexibility^0.4))^0.7, oak landing on 1.00 by
        //construction. Hardness carries the larger exponent because draw effort is dominated by the mass being moved;
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

        //What the grip contributes to holding a bow steady, added to the archer's rangedWeaponsAcc while the bow is
        //drawn. 0.0 is a bare riser.
        //
        //This is the bow's counterpart to speedBonus, and is a separate field rather than a reuse of it because the
        //two are different physical claims. A grip's speedBonus is how much quicker the tool swings, which is about
        //the hand not slipping under load; accuracy is about the bow not moving while it is held at full draw. A
        //wrap can help both, but not by the same amount, and reading one off the other would tie a bow's steadiness
        //to a number tuned for swinging an axe.
        //
        //Where it lands: the engine divides its aim sway by max(1, rangedWeaponsAcc) and caps the reticle at
        //1 - 0.075/acc, so this moves the reticle barely and the hand-shake a great deal. A sturdy grip is roughly
        //a quarter steadier than a bare riser on every bow tier, which is the axis a grip should own - it steadies
        //the hold rather than shrinking the target.
        [JsonProperty]
        public float accuracyBonus = 0.0f;
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
