# Changelog

Personal fork of Toolsmith, maintained for a private server. Versions here are the fork's own
and diverge from upstream after 1.2.20.

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

- The handle tooltip checked only for a creative slot, so a handle shown in the handbook or a
  trader's inventory still had its attributes read. It now uses the same inventory check as
  every other tool part.
- A hardcoded English string on an incomplete tool-part bundle moved into the lang file.
