# Changelog

Personal fork of Toolsmith, maintained for a private server. Versions here are the fork's own
and diverge from upstream after 1.2.20.

## 1.3.2

### Smithing a handle no longer tags whatever else is in the hotbar

Finishing a handle on an anvil could stamp the metal onto a different item entirely - most often a
stack of sticks, which came out reading as tinbronze, carrying that metal's density and a plate
texture. Bones and crude handles were eligible the same way, and a whole stack was stamped at once.

The finished item is a local variable inside the anvil's own code, handed to the player or dropped
on the ground without ever being exposed, so the material was applied by searching the hotbar
afterwards for something that looked like a handle - and a stick is a handle part. The material is
now applied to the finished stack directly, at the moment it is made, which also covers the case
where a full hotbar sends it to the ground instead.

The old search is kept as a fallback for a game update that moves the code out from under the
injection, but it now only accepts the single item the recipe actually produces, and it says in the
log when it runs.

### Bluing grants metalworking experience

Bluing a handle is metalworking the player performed at a forge, but it paid nothing towards the
skill. With XSkills installed it now grants half of what finishing a piece on the anvil grants.

The anvil pays a base amount plus a per-hit amount for every hit landed. Bluing has no hits to
count, so half the base is what carries over - read from the live metalworking config, so a
server that retunes smithing retunes bluing with it. A forge records no owner, since the piece
blues on a forge tick rather than on a player action, so the experience goes to the nearest
player within fifteen blocks; XSkills resolves an unattributed quench the same way.

### Blued handles look blued

Every treatment picked its overlay by whether it went on wet, so bluing darkened a handle exactly
as oil did. Bluing is a colour change - the surface converts to magnetite - so it now has its own
blue overlay, on a handle that is blued and on one that has been oiled over bluing.

### Metal handles sit correctly in wooden tongs

The pose for wooden tongs carried an `origin` that the vanilla rod it was otherwise copied from
does not set, which pivoted the handle away from where the jaws close. The handle is the same
long thin shaft a rod is, so it now uses the rod's transform unchanged.

### Treatments read as what they are

A treated handle read "Treated with Blued", and "Treated with Oiled" for oil. The treatment names
are already adjectives, so the line now states the treatment alone: "Blued.", "Oiled.". Only the
English wording changed; the other languages phrase this as a full sentence that would need
rewriting rather than trimming.

### Oil over bluing keeps the bluing

Oiling a blued metal handle stripped the bluing instead of building on it, leaving a handle with
the stats of oiled wood: a large durability bonus it should not have had, and a third less wear
saving than bluing and oil together are meant to give.

The combined treatment is looked up by deriving its id - the treatment already on the handle, a
dash, then the incoming treatment's stat tag - which for oil over bluing is `blued-oil`. The
stat was named `blued-oiled`, after the English participle rather than the stat tag, so the
lookup always missed and fell through to the plain oil stat. The stat is now named for the tag
it is derived from. The display name is unchanged.

A handle blued and oiled before this fix carries the wrong stats permanently, since the
treatment is recorded when it is applied; re-bluing and re-oiling it corrects them.

### Flax oil treats blued metal handles

Only vanilla flax and olive oil were accepted on a blued handle. Flax oil from Expanded Foods
(`foodoilportion-flax`) and Boric Alchemy's wood finish, which is pressed from flax oil, were
refused despite being the same treatment, so which oil a player happened to have decided whether
the recipe worked at all.

Every flax-derived oil is now accepted on bluing. Boric Alchemy's finish is registered under
`woodfinish`, the code it actually uses - the entries naming it `woodfinish-flax` and
`woodfinish-walnut` matched no item and did nothing on any handle, wood included.

Olive oil no longer treats blued metal. It is a non-drying food oil that goes rancid rather than
curing to a protective film, so it stays a wood treatment. Fat and beeswax remain wood-only.

## 1.3.1

### Metal handles sit in the tongs correctly

A metal handle picked up with tongs rendered at the engine's fallback pose, floating away from
the jaws instead of being gripped. It defined `inForgeTransform`, which places an item lying in
the forge, but neither of the two attributes the tongs actually read: `onTongTransform` and
`onMetalTongTransform`. With both missing, `ItemTongs` falls through to `DefaultTongTransform`.

Both are now set, along with `tongOpening`, which selects the jaw mesh and the two-handed hold
animation. The transform's origin sits at the middle of the shaft, so the jaws close on the
handle's midpoint rather than pivoting it about one end.

## 1.3.0

### Wood hardness for handles

A handle shaped from a support beam now remembers the wood it was made from, and that wood
changes how long the handle lasts. Hardness values are derived from real Janka hardness, with
oak as the 1.00 baseline.

| Wood | Density | Wood | Density |
|---|---:|---|---:|
| Kapok | x0.750 | Birch | x0.980 |
| Pine | x0.825 | Oak | x1.000 |
| Redwood | x0.850 | Maple | x1.025 |
| Bald cypress | x0.850 | Acacia | x1.075 |
| Aged | x0.875 | Purpleheart | x1.200 |
| Larch | x0.925 | Ebony | x1.250 |
| Walnut | x0.950 | | |

- The wood scales the handle's own durability factor before treatment and binding bonuses are
  applied, so a hard wood and a good treatment compound rather than being added up separately.
- Wood density also reaches the binding, but only a binding that is nailed on: metal bindings
  gain up to 10% durability on the hardest woods and lose as much on the softest. Wrapped
  bindings - rope, twine, cord, leather - are unaffected.
- Handles that never had a wood (sticks, bones, crude handles) and handles saved before this
  version are treated as oak.

### Handle tiers rebalanced

- Stick handles: x1.0 to x0.6.
- Crude handles: x1.0 to x0.8.

Both were previously identical to a bone handle. A handle shaped from the softest wood in the
game now still beats a crude one.

### Tooltips

- Every tool tooltip is laid out as a table, with the name of a value on the left and the value
  on the right, rather than as sentences. The table lines are drawn in a monospace font so the
  columns line up; the game's own text font has neither tab stops nor fixed-width digits, so
  nothing else aligns.
- Every number on a tool or part tooltip is colored. Durability and sharpness band by how much
  remains; multipliers and bonuses band by whether they help or hurt.
- A finished tool names the wood of the handle and the material of the binding it was built from.
- Mining speed is listed one material per row rather than as a single run-on line, with each speed
  colored by how fast it is. Tool tier, attack power and reach are laid out in the same table.
- The binding materials had display names declared in their stats files but no text behind them;
  all seventeen are now written out, so a binding can be named rather than only numbered.
- Durability and sharpness are colored by how much remains: purple when untouched or fully
  sharp, green above two thirds, yellow between a third and two thirds, red below a third.
- Handles show their wood and its density, and carry the wood in their name -
  "Carved Tool Handle (Ebony)". A handle with no wood recorded says so rather than claiming oak.
- Tooltip prose shortened throughout. The handbook entries on durability, wear and tool parts
  now explain the states the shortened tooltips no longer spell out.

### Fixes

- A tool built from a head that was sharpened before it was fitted claimed it still needed
  honing. The free-honing notice was shown whenever no honing had been recorded, and the first
  honing is deliberately never recorded, so a sharpened head carried nothing across the craft
  and the finished tool advertised honing it did not need. The notice now also asks whether the
  edge is actually dull, using the same check the whetstone and grindstone already use to decide
  when to stop. The free honing is still there to spend; the tool only mentions it once it is
  worth spending.

- The handle tooltip checked only for a creative slot, so a handle shown in the handbook or a
  trader's inventory still had its attributes read. It now uses the same inventory check as
  every other tool part.
- A hardcoded English string on an incomplete tool-part bundle moved into the lang file.
