using Haft.Config;

namespace Haft.Tests.Support;

//The stat blocks these tests calculate against, transcribed from the shipped JSON under
//assets/haft/config/haft/stats. Kept as builders rather than loaded from those files so a test
//states the numbers it depends on: a test that read the JSON would still pass after a retune that changed
//the balance, which is exactly the change worth being told about.
public static class StatFixtures {

    public static HandleStatDefines Handle(float baseHPfactor, float selfHPBonus, float bindingHPBonus, float speedBonus) =>
        new() { baseHPfactor = baseHPfactor, selfHPBonus = selfHPBonus, bindingHPBonus = bindingHPBonus, speedBonus = speedBonus };

    //handles-vanilla.json
    public static HandleStatDefines Stick() => Handle(0.6f, 0.0f, 0.0f, 0.0f);
    public static HandleStatDefines Carved() => Handle(1.2f, 0.05f, 0.2f, 0.05f);
    public static HandleStatDefines Professional() => Handle(1.5f, 0.1f, 0.4f, 0.1f);
    public static HandleStatDefines Metal() => Handle(2.0f, 0.15f, 0.4f, 0.0f);

    //density and flexibility default to unset (-1.0), the same reading the config gives a material with no bow data:
    //a fixture is a handle material unless a test asks for a limb, and CanMaterialFormLimb rejects it until both are
    //given. Tests that need a limb pass them rather than each building a MaterialStatDefines by hand.
    public static MaterialStatDefines Material(float hardness, float nailBindingBonus, float speedBonus,
                                               float density = -1.0f, float flexibility = -1.0f) =>
        new() {
            hardness = hardness, nailBindingBonus = nailBindingBonus, speedBonus = speedBonus,
            density = density, flexibility = flexibility
        };

    //woods-vanilla.json - oak is the 1.0 baseline on every axis every other material is scaled against.
    public static MaterialStatDefines Oak() => Material(1.0f, 0.0f, 0.0f, density: 1.0f, flexibility: 1.0f);

    //metals-vanilla.json
    public static MaterialStatDefines Copper() => Material(1.3f, 0.06f, -0.17f, density: 2.38f, flexibility: 0.3f);
    public static MaterialStatDefines Iron() => Material(3.0f, 0.12f, -0.15f, density: 2.28f, flexibility: 0.7f);
    public static MaterialStatDefines Steel() => Material(4.55f, 0.17f, -0.15f, density: 2.27f, flexibility: 0.85f);
    public static MaterialStatDefines MeteoricIron() => Material(3.42f, 0.13f, 0.05f, density: 2.28f, flexibility: 0.75f);

    //Dense and soft: the material that motivated splitting hardness from density. A gold limb is heavy enough to
    //throw an arrow and too dead to return the energy, so it reads as a poor bow rather than a great one.
    public static MaterialStatDefines Gold() => Material(0.55f, 0.04f, -0.37f, density: 3.12f, flexibility: 0.1f);

    //A material carrying no bonuses at all, for isolating one term of a calculation from the rest.
    public static MaterialStatDefines NeutralMaterial() => Material(1.0f, 0.0f, 0.0f, density: 1.0f, flexibility: 1.0f);

    public static TreatmentStatDefines Treatment(float handleHPbonus, float chanceToDamageReduction = 0.0f) =>
        new() { handleHPbonus = handleHPbonus, chanceToDamageReduction = chanceToDamageReduction };

    public static TreatmentStatDefines NoTreatment() => Treatment(0.0f);

    public static BindingStatDefines Binding(float baseHPfactor, float selfHPBonus, float handleHPBonus, bool isMetal = false) =>
        new() { baseHPfactor = baseHPfactor, selfHPBonus = selfHPBonus, handleHPBonus = handleHPBonus, isMetal = isMetal,
                metalType = isMetal ? "iron" : null };

    public static BindingStatDefines NoBinding() => Binding(1.0f, 0.0f, 0.0f);

    public static GripStatDefines Grip(float speedBonus, float chanceToDamage, float steadyBonus = 0.0f) =>
        new() { speedBonus = speedBonus, chanceToDamage = chanceToDamage, steadyBonus = steadyBonus };

    public static GripStatDefines PlainGrip() => Grip(0.0f, 1.0f);

    //A grip carrying only the bow axes. speedBonus and chanceToDamage are tool stats and are not read on a bow, so
    //they are left at their neutral values rather than given numbers a reader might take for meaningful.
    public static GripStatDefines BowGrip(float steadyBonus) => Grip(0.0f, 1.0f, steadyBonus);
}
