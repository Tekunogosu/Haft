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

### Proposed new fields

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

## 4. Texture — test this before committing to anything else

This is the cheapest way to find out whether wood-typed bows feel right, and it
is worth doing FIRST as a throwaway test, before any stat work.

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

1. **Texture test (section 4).** Throwaway, answers a visual question cheaply,
   blocks nothing and informs everything.
2. **Coating (section 5).** Ingredient slot exists, stats exist, no new field.
3. **Bamboo removal (section 2).** One patch, but confirm bamboo has no other
   stave-like use first.
4. **Wood species capture (sections 2 and 3).** Needs the `allowedVariants`
   decision settled first, then the three new fields.
5. **String (section 6).** Needs a recipe path from bows to the cordage parts.

Sections 1 and 7 are constraints, not work items. Re-read section 0 before
starting any of this.
