# Toolsmith — remaining work

Written 2026-09-13 at the end of the metal handle session. Everything below was
established by reading the code or testing in game, not from memory. Line numbers
are from the working tree at that date and will drift.

Durable background lives in kb, and is worth reading before starting a section
rather than re-deriving it: `kb goal show 5` for the running journal, and the
notes each section names.

---

## Done this session, for context

The metal handle works end to end: it renders, smiths from an anvil recipe,
carries its metal, and builds into tools that display it. Alongside it, three
systems changed in ways the remaining work depends on.

**Materials are one table.** `toolHandleWoodTag` became `handleMaterialTag`, a
string holding either a wood or a metal, and `WoodStats` became `MaterialStats`
keyed by both. `hardnessFactor` became `densityFactor`. Old saves migrate on read
inside `GetHandleMaterialTag`, so no separate pass exists or is needed.

**Part durability no longer scales off the tool head.** It was
`GetBaseMaxDurability` of the finished tool, which meant the same steel handle
was weak on a copper tool and strong on a steel one. It is now the flat
`ToolsmithConstants.PartDurabilityBase` (1000), so each part's durability comes
from what that part is made of. Both material tables were rebalanced around this:
woods 0.45 to 2.20, metals 0.55 to 4.55, derived from Janka and Brinell hardness.

**Part compatibility runs on tags.** `providesTags` and `requiresTags` on
`ToolsmithPart`, `providesTags` on `ToolsmithStat`, matched by
`ConfigUtility.TagsSatisfy`. Woods provide `wood`/`porous`, metals
`metal`/`smooth`, and every vanilla treatment now requires `wood`. See
[[toolsmith-part-compatibility-runs-on-arbitrary-tags-not-per-rule-fields]].

---

## 1. Metal treatments — DONE, needs testing in game

Built and installed. Not yet verified in play.

The ladder is asymmetric on purpose: metal gets fewer rungs and a stronger top,
and bluing beats oil on wear saving rather than on raw durability, so it reads as
a different kind of protection rather than simply a better one.

    wood   fat (+20% hp, 4% wear) -> wax (+50%, 7%) -> oil (+65%, 10%)
    metal  fat (+20% hp, 4% wear) -> blued (+55%, 15%)

Grease spans both materials. That is partly flavour - it soaks into wood and keeps
rust off steel - and partly a hard constraint: TreatmentParts is keyed by item
code, so fat could not be a wood treatment and a separate metal one.

Wear saving is a new field, `chanceToDamageReduction` on TreatmentStatDefines,
multiplied into the same chanceToDamage a grip feeds:

    chance = grip.chanceToDamage * (1 - treatment.reduction)

Multiplying rather than adding keeps it bounded. The best pairing, sturdy grip on
a blued handle, still lets 55% of damage land.

Bluing is NOT a treatment part. Charcoal is a packing medium heated around the
work, not something rubbed on, so it is a forge interaction: a Harmony postfix on
BlockEntityForge.OnGameTick marks a metal handle blued once it reaches 300C.
There is deliberately no quench - real charcoal bluing air-cools, since the oxide
layer forms while hot and the cooling rate does not matter.

Browning was built and then cut. Vinegar unlocks at roughly the same tech level
as the forge, so browning was dominated the moment it became available: a scarce
liquid and 72 hours to deliver less than bluing, which needs only charcoal and a
heat already to hand. Worth remembering as a design test - a tier nobody would
choose is a decoy, not a tier.

**Two things to verify first, both low confidence.** The forge patch reads a
PRIVATE field named `contents` reflectively, and hooks `OnGameTick`; either could
be wrong for this game version. A one-time error is logged if the field is
missing, so the server log answers it. If bluing never fires and the log is
silent, `OnGameTick` is the wrong hook.

**The trigger, and why it is shaped this way.** Bluing needs a lit forge with fuel
AND a handle that has been cooled first: a piece must fall below 100C to arm, and
only an armed handle blues on reaching 300C. Without the cooling requirement a
handle straight off the anvil blues on its way DOWN from ~900C working heat, which
would mean receiving the finish rather than performing it. Without the lit-forge
requirement, any hot handle touching even a cold empty forge would blue.

Arming is checked inside the forge patch rather than by ticking handles, since a
handle spends nearly all its life outside a forge and the answer is only ever
needed there.

**Fuel type is deliberately unchecked** - any of the seven forge fuels works. The
oxide forms from iron meeting air at 300C; the fuel is a heat source, not a
reagent, so the name charcoal bluing is historical rather than chemical. A
sulphur-based restriction (lignite and contaminated coal stain steel) is
defensible and is a one-line change if ever wanted, but it belongs to a Boric
Alchemy layer rather than the base mod.

**Still open:** bluing happens automatically once the conditions are met, with no
way to decline. Harmless while nothing else needs a hot handle, but section 2
below adds tempering, which does.

## 2. Tempering — deferred to its own session

Decided 2026-09-13: build Toolsmith's own tempering rather than leaning on
Scientific Smithy, and drop that mod's compat entirely. It is outdated and may
not return. Worth reading its implementation for pointers before deleting it.

Do the removal AFTER tempering is built, not before. The compat is dormant rather
than broken - CalculateMaxSharpness falls back cleanly when the attribute is
absent, and the only call that would throw is already gated behind
IsModEnabled("scientificsmithy") - so there is no pressure to rip it out first,
and keeping the risky refactor separate from the new feature means a bug in one
cannot masquerade as a bug in the other. Surface: 24 references across 9 files,
a Compat/ScientificSmithyCompat.cs, and a csproj DLL reference.

Vanilla already ships the data, per metal in worldproperties/block/metal.json:
quenchMinTemp 815, quenchMaxTemp 1050, temperMinTemp 550, temperMaxTemp 700, on
iron, meteoric iron, steel and stainless. `applyquenchablebuffs` is a genuine
vanilla recipe attribute too, set on vanilla tool recipes - but the mechanic
behind it belongs to Scientific Smithy, so the flag does nothing without it.

**The open design question, to settle first.** Tempering needs an effect distinct
from bluing, or it is bluing with extra steps. Real tempering trades hardness for
toughness: less brittle, slightly softer. Candidates - raise durability, reduce
the chance the handle breaks outright, scale the material's density factor, or
gate the higher metals so untempered steel is unusable. Also undecided whether it
is required to make a metal handle at all, or an optional upgrade.

**It forces a change to bluing.** Both mechanics are "heat the handle in a forge",
and bluing currently fires automatically at 300C, so a handle heated to temper
would blue on the way past. Bluing has to become a deliberate action before
tempering can share the forge.

On cost: this began as a way to make the handle expensive in process rather than
metal, since the pattern cannot carry a two-bar cost without an absurd shape. If
tempering ends up not filling that role, widening the pattern is the fallback.

## 3. Adhesive grips — DONE, needs testing

Built and installed. Not yet verified in play.

A grip backed with an adhesive is the same item carrying a `gripAdhesiveTag`
attribute, not a treated variant. The metal handle declares
`requiresTags: ["adhesive-backed"]`, so it refuses a plain grip and accepts a
backed one; every wooden handle requires nothing and is unaffected.

This needed the tag check to run in BOTH directions. It already asked whether a
grip's requirements were met by the handle; it now also asks whether the handle's
requirements are met by the grip. The grip's side is answered from the STACK via
GetGripProvidedTags rather than from its part define, because the adhesive is an
attribute and a part define would never see it.

Recipes are generated, one per grip per adhesive per liquid container, in
GenerateAdhesiveGripRecipes. The adhesives are read from the BINDING parts whose
stat tag is "glue" rather than from a list of their own, so vanilla pitch glue and
Butchery's sinew and hide glues are all picked up, and a mod adding another glue
gets this for free.

Two Harmony patches were needed because grips carry no behaviour of their own and
attaching one to roughly 130 items for a single recipe group would cost more than
it is worth: one on ConsumeCraftingIngredients to stamp the attribute, keyed on
the recipe group, and one on GetHeldItemInfo so a backed grip says so rather than
being indistinguishable from a plain one.

Which adhesive was used is stored rather than a bare flag, since that is what a
tint would key off and what a later mechanic would read to tell hide glue from
pitch. The tint idea from the original design is NOT built.

## 4. Boric Alchemy compatibility — clear-shot, after section 1

Boric Alchemy (`boricalchemy`) ships genuine metal-finishing reagents that vanilla
has no equivalent for: potassium dichromate and chromic acid for bluing and
passivating, phosphoric acid for parkerizing, caustic potash for a hot-bluing
salt bath, mineral oil as the post-bluing seal, activated charcoal as the carbon
source for case-hardening.

Every item and liquid it adds is plain Json inside the zip, so reading what it
provides needs `unzip`, not a decompiler. Only its machine behaviour is in the
dll.

The split to aim for, revised once browning was cut: Boric Alchemy should build
ON TOP of bluing rather than offering a parallel chemical ladder. A blued surface
is the base, and its reagents refine or passivate that surface further. This
keeps the vanilla path complete on its own - the basics must never be gated behind
another mod - while giving the chemistry somewhere to go that is not simply a
bigger number.

Phosphoric acid over a blued finish is parkerizing, and potassium dichromate
passivates it; both are genuine follow-on processes rather than alternatives,
which is exactly the shape wanted here.

Gating rule, learned the hard way this session: stat and part configs under
`compatibility/<modid>/` load recursively whether or not the mod is present, and
that is harmless, because a stat block is only ever reached by id. Do not add a
loader for them — doing so caused duplicate-entry errors. Only *recipes* need a
`dependsOn` patch under `patches/compatability/`, because a recipe naming a
nonexistent ingredient fails to resolve. Mind the spelling difference between the
two directories.

See [[boric-alchemy-a-chemistry-mod-whose-assets-are-plain-json-no-decompiling-needed]]
and [[toolsmith-compatibility-config-files-load-recursively-only-recipes-need-dependson-gating]].

---

## 5. Meteoric iron speed bonus — DONE, needs testing

Built and installed. Not yet verified in play.

`speedBonus` now exists on MaterialStatDefines and is a third term in
CalculateSpeedBonus alongside the handle tier and the grip. All three add, so a
material penalty can be offset by a good grip rather than being an inescapable
tax on using metal.

The metal TIER penalty of -0.15 was moved onto the materials. Leaving it in both
places would have taxed every metal twice and cancelled meteoric iron's bonus with
its own tier.

Values come from real density in g/cm3 rather than from hardness, since weight is
what makes a handle slow to swing and that is a different axis from durability.
Iron at 7.87 is the reference and keeps the old -0.15, so nothing regresses;
everything else scales from there. Gold at 19.3 lands on -0.37.

Meteoric iron is the deliberate exception at +0.05 - the only metal that swings
faster than neutral. Its density alone would have given it roughly -0.15 like
iron, so the bonus is a design choice rather than a derivation.

+5% is a flat chosen value, not derived from anything and not tied to any other
number. It makes meteoric iron the best-handling metal by a clear margin while
keeping the swing modest, since the metal's real advantage is durability - it
carries 3.42x oak's density factor. Change it freely if it feels wrong in play.

Woods carry no speedBonus at all and default to 0.0, so a wooden handle's speed
still comes from its tier rather than its species.

## 6. Chromium and stainless steel production — open, own session

Tracked as kb Todo #4. The user has said the mod's scope is broadening enough
that metalworking additions would now fit, but that this is its own session.

Vanilla ships the front half of a chromium chain and no back half: chromite ore
generates, a barrel recipe crushes and dilutes it, and the only consumer is
mordant cloth for dyeing. Nothing turns it into metal. Stainless steel, chromium,
titanium and platinum all appear only in recipes that *consume* their ingots.

The cheapest honest route is two recipes on existing systems: crushed chromite
reduces to a chromium ingot, then chromium plus iron as a ratio-based alloy
recipe in `recipes/alloy/`, real stainless being iron with about 11% chromium.

Two cautions. This is content vanilla deliberately left unfinished, so a later
version implementing it could conflict. And a metal production chain is a
different kind of mod from tool tinkering — it may belong in its own mod that
Toolsmith carries compat for, the way uraniumexpanded does.

See [[stainless-steel-chromium-and-titanium-are-unfinished-in-vanilla-vintage-story]].

---

## Loose ends worth a few minutes

`lang/en.json` has a trailing comma before its closing brace, present at HEAD and
not introduced by this session's work. Vintage Story's parser tolerates it, but
strict Json tooling rejects the whole file — including the Cake build's own
validation pass, which is why it is worth fixing.

The `enabled` field on `ToolsmithPart` is not honoured anywhere in the code, and
`ToolsmithStat` has no such field. Either implement it or drop it; as it stands it
reads like a gate that works.

The handle's own tooltip now says `Handle: Oak` while sitting on a handle, which
is mildly redundant. It shares a lang key with the tool tooltip, where the label
is correct and matches the `Binding:` line below it. Splitting the key is easy if
it grates in play.

Nothing has been committed this session. The working tree carries all of the
above.
