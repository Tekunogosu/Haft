# Toolsmith — bows

Written 2026-09-13, end of the weapons-review session. Design only; nothing in
here is built. Everything below was established by reading the game assets and
Toolsmith's own configs at that date, not from memory. Paths and line numbers
will drift.

Read `NEXT-SESSION.md` first — the metal-handle work it describes is still
unverified in play, and section 0 below explains why that blocks this.

---

## 0. Do not start this until the current build is tested

`TESTING.md` has one checked box out of roughly thirty. Sections 1, 3 and 5 are
all "built, not verified", and nothing is committed. Bows add a second
unverified layer on top of the first, which means a failure in play could belong
to either. Test, commit, then start here.

---

## 1. Why the spear works and a bow does not

The spear is not a weapon integration. It is one regex entry —
`config/toolsmith/regex/tinkerabletools/tinkerabletools-spears.json` contains
the single string `"spear"`. Everything else fell out of the existing tool
pipeline because the vanilla spear already has the shape Toolsmith requires:

    spear.json:   spearhead-{metal}  +  stick   ->  spear-generic-{metal}
                  ^ matches "head"      ^ handle

`RecipeRegisterModSystem.AssetsFinalize` (around line 59) scans every grid
recipe for one whose OUTPUT is on the tinkerable list and whose INGREDIENTS
contain a tool head, matched by `toolheads-base.json` = `["head", "blade"]`. The
spear satisfies both, so it gained a head, a handle, durability decoupling,
treatments and grips without new code. The only genuine C# work was damage-path
plumbing for the thrown case (`TinkeringUtility.cs:245-427`) — a thrown spear is
damaged through a `DummySlot` with no inventory.

A bow satisfies neither half. Three blockers:

1. **No tool-head ingredient.** `bow-crude` and `bow-simple` are `stick + rope`.
   `bow-long` is `bowstave-long-dry + flaxtwine + leather + coating`. Nothing
   matches `head` or `blade`, so the loop never fires and adding `"bow"` to the
   tinkerable regex accomplishes nothing on its own.
2. **`class: "ItemBow"`, not a plain item.** Toolsmith's part model hangs off
   `CollectibleBehaviorTinkeredTools` and `ModularPartRenderingFromAttributes`
   (`ToolsmithModSystem.cs:254-258`), which compose a tool from head/handle/
   binding shapes. A bow's shape is one staged mesh with four charge alternates
   (`{type}-charge1..3`) driven by aim state. There is no head-and-handle
   decomposition to render.
3. **Durability is the wrong axis.** A bow has no edge, no sharpness, and
   nothing to grind. It already carries per-type durability (180-750) and
   `statModifierByType: rangedWeaponsAcc`.

**Consequence for the whole design:** bows should NOT be forced through the
tinkerable-tool pipeline. They want attributes on the finished item, read by
the existing stat tables, and not the head/handle/binding renderer. This is the
single most important decision in this document.

One convenient accident: `bowstave.json` already lives in
`survival/itemtypes/toolhead/`, so the *path* says toolhead even though the
item is not one. Do not read anything into that; the regex matches item CODE,
not asset path, and `bowstave` contains neither `head` nor `blade`.

---

## 2. The bowstave, and the two recurve paths

Corrected 2026-09-13 after reading `itemtypes/toolhead/bowstave.json`. An
earlier reading of this session claimed recurve staves come only from bamboo.
That is wrong. There are TWO routes to a recurve stave:

    log-placed-{oak|maple|walnut} x2  + saw  ->  bowstave-long-raw
    bamboostakes x2                   + saw  ->  bowstave-recurve-raw
    bowstave-long-raw                 + saw  ->  bowstave-recurve-raw

The third line is the one that matters: a long stave can be sawn down into a
recurve stave, so recurve bows are reachable from ordinary logs. The same
conversion also exists as a `GroundStoredProcessable` behavior on
`*-long-raw` (processTime 4, saw), so it can happen on the ground as well as in
the grid.

Both raw staves then dry over 168 hours via `transitionableProps` into
`bowstave-{type}-dry`, which is what the bow recipes consume.

**Decision taken: drop the bamboo route.** It does not fit — bamboo is a grass,
it has no entry in `worldproperties/block/wood.json`, it carries no wood
variant to key off, and supporting it would mean inventing a species that the
existing `providesTags: ["wood", "porous"]` treatment compatibility only
half-describes. Removing the `bamboostakes -> bowstave-recurve-raw` recipe
leaves recurve fully reachable through the long stave, so nothing becomes
uncraftable.

Two cautions on the removal. It is a vanilla recipe, so it needs a patch that
disables rather than deletes, and it is a genuine balance change for anyone who
was using bamboo as a shortcut in a jungle start. Check whether bamboo has any
other stave-like use before cutting it.

**The species is currently thrown away.** `bowstave.json`'s recipe reads
`log-placed-*-ud` with `name: "wood"` and `allowedVariants: ["oak", "maple",
"walnut"]`, and then outputs a plain `bowstave-long-raw` with no variant and no
attribute. The wood is known at craft time and discarded one line later. That
is the hook for everything in section 3.

---

## 3. Wood type — the design, settled

Three species is too few and they are the boring middle of the table. The
`allowedVariants` list needs widening for the tradeoff to exist at all; see the
open question at the end of this section.

### Why `densityFactor` is the wrong field

`woods-vanilla.json` carries `densityFactor`, derived from Janka hardness, 0.45
(kapok) to 2.20 (ebony). Janka measures resistance to denting. A bow limb needs
elasticity and resistance to taking a set, which is a different property and
frequently an opposed one. Reusing `densityFactor` would make ebony the best
bow in the game; ebony is brittle and makes a terrible bow.

This is the same situation as `speedBonus` in the metal-handle session: a second
axis needs a second field, not a reinterpretation of the first. Follow that
precedent.

### The two real properties

- **MOE (modulus of elasticity, stiffness)** — a stiffer limb stores more energy
  per inch of draw. More power, but a heavier draw weight and slower to reach
  full draw.
- **Elastic limit** — how far the wood bends before taking a permanent set.
  This is the property that makes a wood a bow wood.

The classic bow woods (yew, osage, hickory) are not the stiffest. They have the
best ratio of elastic limit to stiffness. That ratio is the tradeoff axis, and
it orders the woods almost opposite to Janka.

### Vanilla woods and their counterparts

Ordered by suitability as a bow. Janka column is the existing `densityFactor`,
shown for contrast.

| Wood | Real counterpart | Janka | MOE (GPa) | Bow behaviour |
|---|---|---|---|---|
| larch | *Larix*, real Alpine/Siberian bow wood | 0.72 | 13.8 | Springy, light, forgiving |
| birch | *Betula*, Finnish/Saami bow wood | 0.97 | 13.9 | Fast, light, low draw weight |
| maple | *Acer*, standard recurve core/laminate | 1.12 | 12.6 | The all-rounder |
| oak | *Quercus*, usable but ring-porous | 1.00 | 12.3 | Middling, takes a set |
| acacia | *Acacia*/*Robinia*; black locust is top-tier | 1.36 | 14.1 | High power, demanding draw |
| walnut | *Juglans*, gunstock wood | 0.85 | 11.6 | Low power, easy draw |
| purpleheart | *Peltogyne*, real laminate bow wood | 1.75 | 20.3 | Most power, brutal draw |
| pine | *Pinus*, not a bow wood | 0.62 | 9.0 | Weak, fast |
| redwood | *Sequoia*, brittle | 0.50 | 9.2 | Weak and fragile |
| baldcypress | *Taxodium*, rot-resistant | 0.55 | 9.9 | Weak, damp-resistant niche |
| kapok | *Ceiba*, balsa-like | 0.45 | 3.7 | Should barely function |
| ebony | *Diospyros*, brittle inlay wood | 2.20 | 16.3 | Trap: hits hard, snaps |

`aged` is salvaged wood with no tree and no bow counterpart. Reclaimed timber is
dry, checked and unreliable. Either exclude it from staves or give it poor
values; do not let it inherit oak's defaults silently.

### BUILT 2026-09-15 — one field, three derivations

Superseded the three-field proposal below. Bows read the SAME `MaterialStats`
table handles do; the only addition is `flexibility` (stiffness/springback,
oak = 1.00, derived from real MOE the way `densityFactor` is from Janka).

    draw weight     = densityFactor * flexibility   (multiply: a limb needs both)
    draw speed      = speedBonus                    (unchanged; lighter = easier to draw)
    limb durability = flexibility                   (density is NOT a term here)

Helpers are `CalculateBowDrawWeight` / `BowDrawSpeed` / `BowLimbDurability`
plus `CanMaterialFormLimb` in `HaftPartStatsHelpers`. `flexibility` unset
(-1.0) means "cannot form a limb" and is deliberately never defaulted, so a
compat mod's materials do not silently rate as well as oak.

Computed values, all 31 materials, ordered by draw weight:

| Material | density | flex | draw | speed | limb dur |
|---|---|---|---|---|---|
| steel | 4.55 | 0.85 | 3.87 | -0.15 | 0.85 |
| ebony | 2.20 | 1.33 | 2.93 | -0.04 | 1.33 |
| purpleheart | 1.75 | 1.65 | 2.89 | -0.015 | 1.65 |
| meteoriciron | 3.42 | 0.75 | 2.56 | +0.05 | 0.75 |
| iron | 3.00 | 0.70 | 2.10 | -0.15 | 0.70 |
| acacia | 1.36 | 1.15 | 1.56 | -0.005 | 1.15 |
| nickel | 2.58 | 0.55 | 1.42 | -0.17 | 0.55 |
| blackbronze | 2.43 | 0.48 | 1.17 | -0.17 | 0.48 |
| cupronickel | 2.29 | 0.50 | 1.15 | -0.17 | 0.50 |
| maple | 1.12 | 1.02 | 1.14 | +0.005 | 1.02 |
| birch | 0.97 | 1.13 | 1.10 | +0.015 | 1.13 |
| oak | 1.00 | 1.00 | 1.00 | 0.00 | 1.00 |
| tinbronze | 2.15 | 0.45 | 0.97 | -0.14 | 0.45 |
| larch | 0.72 | 1.12 | 0.81 | +0.02 | 1.12 |
| walnut | 0.85 | 0.94 | 0.80 | +0.02 | 0.94 |
| bismuthbronze | 2.00 | 0.40 | 0.80 | -0.15 | 0.40 |
| brass | 1.73 | 0.35 | 0.61 | -0.16 | 0.35 |
| pine | 0.62 | 0.73 | 0.45 | +0.025 | 0.73 |
| aged | 0.70 | 0.65 | 0.45 | +0.02 | 0.65 |
| baldcypress | 0.55 | 0.80 | 0.44 | +0.03 | 0.80 |
| copper | 1.30 | 0.30 | 0.39 | -0.17 | 0.30 |
| redwood | 0.50 | 0.75 | 0.38 | +0.045 | 0.75 |
| bismuth | 2.01 | 0.14 | 0.28 | -0.19 | 0.14 |
| molybdochalkos | 1.58 | 0.15 | 0.24 | -0.18 | 0.15 |
| kapok | 0.45 | 0.30 | 0.14 | +0.07 | 0.30 |
| zinc | 0.74 | 0.18 | 0.13 | -0.14 | 0.18 |
| electrum | 1.11 | 0.12 | 0.13 | -0.29 | 0.12 |
| tin | 1.47 | 0.08 | 0.12 | -0.14 | 0.08 |
| silver | 0.55 | 0.12 | 0.07 | -0.20 | 0.12 |
| gold | 0.55 | 0.10 | 0.06 | -0.37 | 0.10 |
| lead | 1.04 | 0.05 | 0.05 | -0.22 | 0.05 |

`aged` at 0.65 is judgement, not derivation — salvaged timber has no species
and no MOE figure. Marked as such in the config.

Metal flexibility is scaled by usability as an UNTREATED spring, not raw
Young's modulus: a metal that yields rather than springing back takes a set on
the first draw. Steel is deliberately held at 0.85, below what a spring-
tempered limb should reach, leaving headroom for a temper treatment.
`TreatmentStatDefines` already gates on material tags (`"wood|blued"`), so
that treatment has a home — but it carries only `handleHPbonus` and
`chanceToDamageReduction` today, so a flexibility-modifying field is still
needed before spring steel works.

**Settled 2026-09-15: the numbers stand as computed. No curve, no cap.**

High draw weight already pays for itself in low draw speed, and that is the
intended shape rather than a balance problem to correct. A steel bow at 3.87
draw / -0.15 speed is a siege bow: it may drop a moose in one or two shots,
and the moose may reach you before the second arrow is nocked. The tradeoff is
power against time-to-second-shot under threat, which is a real decision in
the moment, not two numbers to compare on a table.

This is why draw weight is deliberately NOT capped or curved. A cap would
flatten exactly the extreme that makes the choice interesting, and the drawn
-out reload is the cost that was already priced in.

Ebony (2.93) edging purpleheart (2.89) is likewise left alone. Ebony's trap is
not that it hits softly - it is that it is the worst-draining bow in the table
to actually shoot (-0.04 speed) and takes a set fastest of the heavy woods
(1.33 limb durability against purpleheart's 1.65). A player who grabs it for
the big damage number gets a bow that is unpleasant to use and wears out,
which is the lesson intended.

Metals are kept even where a wood dominates them, for parity: a material that
has a handle value should have a bow value, so nothing looks arbitrarily
missing. Dropping the unusable ones stays available if they prove to be pure
noise in play.

### Superseded proposal — three bow-only fields (NOT built)

    drawWeight     -> arrow damage / cast     (from MOE)
    drawSpeed      -> time to reach full draw (inverse of stiffness)
    limbDurability -> resists taking a set    (elastic limit, NOT Janka)

| Wood | drawWeight | drawSpeed | limbDurability | Reads as |
|---|---|---|---|---|
| purpleheart | +0.35 | -0.30 | 1.30 | Siege bow: max power, slow |
| acacia | +0.25 | -0.15 | 1.45 | Best all-round if you accept the draw |
| ebony | +0.20 | -0.25 | 0.60 | Trap: hits hard, breaks |
| maple | +0.10 | 0.00 | 1.35 | Baseline recurve wood |
| birch | 0.00 | +0.15 | 1.20 | Fast and light |
| larch | -0.05 | +0.20 | 1.40 | Best durability/speed, low power |
| oak | 0.00 | 0.00 | 1.00 | Reference |
| walnut | -0.10 | +0.10 | 0.90 | Weak, easy |
| baldcypress | -0.20 | +0.10 | 0.85 | Damp-resistant niche |
| pine | -0.25 | +0.20 | 0.70 | Starter |
| redwood | -0.30 | +0.15 | 0.60 | Poor |
| kapok | -0.40 | +0.25 | 0.40 | Barely a bow |

Oak is pinned at 1.00 / 0.00 / 0.00 as the reference, matching how iron anchors
the metals table, so nothing regresses.

Ebony is the deliberate trap and the one entry worth defending. A player who
assumes the hardest wood is the best bow gets a bow that hits hard and breaks.
That rewards knowing something real, which is the same instinct that cut
browning last session.

These numbers are chosen to be legible, not derived from a formula. Treat them
as a starting point to feel out in play.

**Open question, to settle before implementing.** Vanilla allows only oak,
maple and walnut as stave woods, and those three span just -0.10 to +0.10
drawWeight — the tradeoff barely exists inside them. Widening `allowedVariants`
to reach larch, acacia, purpleheart and the weak woods is a one-line patch but
a real vanilla balance change. The alternative is accepting that the
interesting woods are unreachable, which makes the whole system nearly
pointless. Recommend widening, but it is a decision, not a default.

---

## 4. Texture — TESTED, works

Result, 2026-09-15: all four bow tiers render Haft's tool-wood textures
correctly. Handle-authored textures tile acceptably across a long limb, which
was the open visual risk. The wood axis is viable; section 3's stat design is
not blocked on appearance.

Still unmeasured: whether the species are distinguishable *at a glance* as
opposed to correct when compared side by side. That governs how much the
tradeoff can lean on visual identification alone.

The test patch is `assets/haft/patches/bow-woodtexture-test.json` (crude→pine,
simple→walnut, long→purpleheart, recurve→larch). It is a throwaway probe, not
a feature — it hardcodes one species per tier. Delete it when the real
mechanism lands; see the note at the end of this section.

Two corrections to the original table below, both established by reading the
shape files rather than inferring from names:

- Wood-bearing keys per shape are `maple` (crude), `maple`+`handle` (simple),
  `aged`+`bone` (long), `aged` (recurve). The one-key-per-shape table below
  was incomplete.
- `bone` means different materials on different tiers. On `bow-long` it points
  at `block/wood/debarked/oak` — a wood slot with a misleading name. On
  `bow-recurve` it points at `block/creature/bone` and is a real, visible bone
  element on the outside of the limb, which makes it a candidate slot for
  metal-tipped recurve variants. Any future patch touching `bone` must be
  `texturesByType`-scoped or it will paint long-bow limb wood as metal.

Do NOT set `"side"` on these patches. It defaults to `Universal`; setting it
to `client` makes the patch silently fail, because item type JSON is loaded
and patched server-side and then synced. See kb Note #67.

**The patch is the wrong long-term mechanism.** Haft already does per-species
wood through the `haft:ModularPartRenderingFromAttributes` behavior
(`carpentedhandle.json`, `metalhandle.json`), which generates one itemstack
per species and stores the choice as a per-stack attribute — which is what the
stat work in section 3 has to key off anyway. Attaching it to bows is blocked
on one thing: its `GenMesh` resolves a single `item.Shape.Base`
(`ModularPartRenderingFromAttributes.cs:156`) and has no concept of the four
charge alternates `ItemBow` swaps between while drawing. Teaching it about
alternates is the real step. See kb Note #68.

### Original notes (kept for the shape/texture-path reference)

Toolsmith already ships all thirteen wood textures at
`assets/toolsmith/textures/block/tools/tool{wood}.png` — acacia, aged,
baldcypress, birch, ebony, kapok, larch, maple, oak, pine, purpleheart,
redwood, walnut. The path convention is already a constant:
`ToolsmithConstants.HandleWoodTexturePathMinusType = "toolsmith:block/tools/tool"`,
with the species appended. `carpentedhandle.json` shows the existing pattern of
listing them as texture alternates.

Each vanilla bow shape exposes exactly one named wood texture key, which an item
JSON can override without touching the shape:

| Shape | Wood texture key | Currently points at |
|---|---|---|
| `bow/simple` | `maple` | `block/wood/debarked/maple` |
| `bow/crude` | `maple` | `block/wood/debarked/maple` |
| `bow/long` | `aged` | `block/wood/debarked/aged` |
| `bow/recurve` | `aged` | `block/wood/debarked/aged` |

The staves are the same story: `bowstave-long-raw` and `-recurve-raw` define an
`aged` key pointing at `block/wood/debarked/aged`.

So a retexture is a patch on the bow and bowstave item JSON replacing that one
key with `toolsmith:block/tools/tool{wood}`. Note the key NAME stays whatever
the shape calls it (`maple` on simple/crude, `aged` on long/recurve) — the key
is a slot name in the shape, not a species, and renaming it would break the
mapping. That is a confusing detail worth writing down now: on a long bow you
will be setting a key literally named `aged` to an oak texture.

Two things this test answers cheaply: whether thirteen wood tints are visually
distinguishable at bow scale, and whether the handle textures (authored for
small tool handles) tile acceptably across a much longer limb. If they look
bad, the stat design still stands but the payoff is weaker, and that is worth
knowing before building it.

`bow-long` also has `oak` and `acacia` listed in `textureSizes` but not in
`textures` — leftovers. Harmless, but do not be misled into thinking the shape
already supports per-species wood.

---

## 5. Coating / treatment — clear-shot, after wood

Vanilla already asks for a coating and then ignores which one you used.
`bow-long` takes `F: { type: "item", code: "{coating}" }` with
`allowedVariants: { coating: ["fat-rendered", "resin", "beeswax"] }`, all three
interchangeable with no mechanical difference.

Toolsmith's `TreatmentStatDefines` ladder already covers two of the three codes:

    fat  handleHPbonus 0.20  chanceToDamageReduction 0.04
    wax  handleHPbonus 0.50  chanceToDamageReduction 0.07
    oil  handleHPbonus 0.65  chanceToDamageReduction 0.10

`resin` has no Toolsmith entry and would need one. Real bow finishes are wax,
resin/pitch and oil, so all three are defensible; resin sits naturally between
fat and wax.

No new field is required — `handleHPbonus` maps onto limb durability and
`chanceToDamageReduction` onto wear, which is what a finish actually does for a
bow. This is the lowest-effort real feature in the document.

---

## 6. String — the most interesting, the most work

Real bowstring care is about the string surviving, not the arrow flying harder:

- Waxing is mandatory, not an upgrade. Linen, hemp and sinew strings are waxed
  to bind the fibres, shed water and resist abrasion. Unwaxed natural string
  frays and snaps.
- A wet string stretches and loses cast. Historically the string came off in
  rain before the bow did.
- Serving — a sacrificial wrap at the nocking point and loops — is the wear part
  and is designed to be replaced.

So the split is **material -> performance, treatment -> wear**, the same shape
already built for handles.

**String material should not give damage.** The limbs store the energy; the
string only affects how efficiently it transfers. A flat damage bonus also
collides with `damageByType`, which already separates the four bow tiers. The
two honest knobs:

- low stretch -> better cast and consistency -> `rangedWeaponsAcc`, which
  already exists in `statModifierByType`
- low mass -> better cast -> draw speed

Sinew is strong and low-stretch but heavy and hates damp. Linen is light and
consistent, the historical target-archery choice. Silk is lightest. Rawhide is
crude and stretchy. That is a genuine multi-axis tradeoff rather than a ladder,
which matters — the browning lesson from last session applies directly. If one
string is better on every axis the others are decoys.

**Supply problem.** Vanilla ships exactly two string items, `flaxtwine` and
`rope`. That is not a ladder. But Toolsmith's own `bindings-vanilla.json`
already defines a full cordage progression with `"bindingShapePath": "string"`
— reeds 1.0, twine 1.2, hide 1.25, rope 1.25, cloth 1.3, leather 1.5, sturdy
1.8 — and a `bindings-stringsense.json` compat file already exists. The binding
system IS the string system; the parts only need to be reachable from a bow
recipe. Reuse `BindingStats` rather than inventing a parallel table.

---

## 7. Constraints that apply to every section

**`requiresTrait: "bowyer"`** gates `bow-crude` and `bow-recurve` in
`recipes/grid/tool/bow.json`. Any generated or patched recipe must preserve it,
or class-gated content silently unlocks. This is easy to lose when generating
recipe variants in code.

**`bow-crude` and `bow-simple` are `stick + rope`** with no species anywhere, so
any wood system covers only `bow-long` and `bow-recurve`. Sticks carry no wood
variant in vanilla. Either accept that the bottom two tiers are plain, or find a
species source for sticks, which is a much larger change.

**`bow-recurve` also needs `bushmeat-raw` and `bone`**, and is the only bow
requiring both a trait and those reagents. It is the top tier; treat it as the
payoff rather than the starting point.

---

## Suggested order

Resolution distance ascending, which is not the same as the section order above.

1. ~~**Texture test (section 4).**~~ DONE 2026-09-15 — wood reads correctly on
   all four tiers. The throwaway patch is still in the tree; the real
   mechanism is the behavior described at the top of section 4.
1b. ~~**Wood species capture and stats.**~~ DONE 2026-09-15, verified in play.
   `flexibility` on MaterialStats, three derivations, capture at stave craft,
   carried across drying, tooltips on staves and bows. `/finishTransition`
   (`/ft`) completes a held item's transition instantly for testing.
   Remaining: nothing reads the stats for actual bow BEHAVIOUR yet - draw
   power, speed and limb life are displayed but do not affect shooting.
2. **Coating (section 5).** Ingredient slot exists, stats exist, no new field.
3. **Bamboo removal (section 2).** One patch, but confirm bamboo has no other
   stave-like use first.
4. **Wood species capture (sections 2 and 3).** Needs the `allowedVariants`
   decision settled first, then the three new fields.
5. **String (section 6).** Needs a recipe path from bows to the cordage parts.

Sections 1 and 7 are constraints, not work items. Re-read section 0 before
starting any of this.
