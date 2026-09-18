# Haft

Haft lets you upgrade your tools and weapons to greatly extend their use, and you keep the
head when one finally breaks. It diverges from the original Toolsmith in three ways: the wood
you pick for a handle matters, metal as a handle option with its own treatments, and weapons
can be tinkered as well. Handle material drives both durability and swing speed, weighted by
the density of the wood, and every material value is derived from the real-world properties of 
the material.


## Wood


### Stats

Handles and bow limbs read the same material table. Oak is the reference material and sits at
`1.00` on every multiplier axis.

| Wood | Hardness | Density | Speed | Nail Binding | Flexibility | Draw Speed |
|---|---|---|---|---|---|---|
| Kapok | 0.45 | 0.68 | +0.070 | −0.10 | 0.30 | 0.33 |
| Redwood | 0.50 | 0.82 | +0.045 | −0.09 | 0.75 | 0.47 |
| Baldcypress | 0.55 | 0.87 | +0.030 | −0.08 | 0.80 | 0.52 |
| Pine | 0.62 | 0.87 | +0.025 | −0.07 | 0.73 | 0.57 |
| Aged | 0.70 | 0.92 | +0.020 | −0.05 | 0.65 | 0.62 |
| Larch | 0.72 | 0.92 | +0.020 | −0.04 | 1.12 | 0.75 |
| Walnut | 0.85 | 0.96 | +0.020 | −0.02 | 0.94 | 0.84 |
| Birch | 0.97 | 0.96 | +0.015 | −0.01 | 1.13 | 1.00 |
| Oak | 1.00 | 1.00 | 0.000 | 0.00 | 1.00 | 1.00 |
| Maple | 1.12 | 0.98 | +0.005 | +0.02 | 1.02 | 1.12 |
| Acacia | 1.36 | 1.02 | −0.005 | +0.05 | 1.15 | 1.41 |
| Purpleheart | 1.75 | 1.07 | −0.015 | +0.08 | 1.65 | 1.99 |
| Ebony | 2.20 | 1.15 | −0.040 | +0.10 | 1.33 | 2.35 |

- **Hardness** multiplies handle durability. Harder material lasts longer. Wood values are
  derived from real Janka hardness, with oak at `1.00`.
- **Density** is mass, derived from real density as `(g/cm³ / 0.75)^0.35`. It drives bow draw
  weight — the mass a limb throws — and is deliberately a separate axis from hardness.
- **Speed** is added to the tool's swing speed, stacking additively with the grip.
- **Nail Binding** adjusts binding durability, and applies **only to nailed (metal) bindings** —
  a rope or twine wrap is tightened around the handle rather than driven into it, so handle
  hardness makes no difference to how well it holds.
- **Flexibility** is a bow limb's springback. Draw weight is density × flexibility, so a
  material needs both mass and springback — ebony is the hardest wood here but not the best bow.
- **Draw Speed** multiplies the time a shot needs to reach full draw, so **lower is faster**.


### Treatments


## Metal


### Stats

Metal handles read the same axes as wood, on the same oak-is-`1.00` scale. Metal buys
durability at the cost of swing speed: nearly every metal carries a speed penalty that a good
grip can offset but never fully erase.

| Metal | Hardness | Density | Speed | Nail Binding | Flexibility | Draw Speed |
|---|---|---|---|---|---|---|
| Gold | 0.55 | 3.12 | −0.370 | +0.04 | 0.10 | 0.29 |
| Silver | 0.55 | 2.52 | −0.200 | +0.04 | 0.12 | 0.31 |
| Zinc | 0.74 | 2.20 | −0.140 | +0.05 | 0.18 | 0.46 |
| Lead | 1.04 | 2.59 | −0.220 | +0.06 | 0.05 | 0.45 |
| Electrum | 1.11 | 2.82 | −0.290 | +0.06 | 0.12 | 0.61 |
| Copper | 1.30 | 2.38 | −0.170 | +0.06 | 0.30 | 0.92 |
| Tin | 1.47 | 2.22 | −0.140 | +0.07 | 0.08 | 0.72 |
| Molybdochalkos | 1.58 | 2.40 | −0.180 | +0.07 | 0.15 | 0.92 |
| Brass | 1.73 | 2.34 | −0.160 | +0.08 | 0.35 | 1.28 |
| Bismuth Bronze | 2.00 | 2.35 | −0.150 | +0.09 | 0.40 | 1.53 |
| Bismuth | 2.01 | 2.46 | −0.190 | +0.09 | 0.14 | 1.14 |
| Tin Bronze | 2.15 | 2.37 | −0.140 | +0.09 | 0.45 | 1.69 |
| Cupronickel | 2.29 | 2.38 | −0.170 | +0.10 | 0.50 | 1.86 |
| Black Bronze | 2.43 | 2.38 | −0.170 | +0.10 | 0.48 | 1.94 |
| Nickel | 2.58 | 2.38 | −0.170 | +0.11 | 0.55 | 2.14 |
| Iron | 3.00 | 2.28 | −0.150 | +0.12 | 0.70 | 2.66 |
| Meteoric Iron | 3.42 | 2.28 | **+0.050** | +0.13 | 0.75 | 3.08 |
| Steel | 4.55 | 2.27 | −0.150 | +0.17 | 0.85 | 4.22 |

Meteoric iron is the one metal with a positive speed bonus. Steel is the most durable handle
material in the mod at `4.55` hardness, roughly double ebony.

The two columns come apart hardest at the top of this table, which is the point of keeping them
separate. Gold is the densest material in the mod and the softest: a gold handle wears out
faster than pine, and a gold bow is heavy enough to throw an arrow but far too dead to return
the energy, giving it a draw weight of `0.31` against oak's `1.00`. Weight is not strength.


### Treatments



## Grips and Bindings

Grips and bindings are scored on their own axes, independent of the handle material.

### Grips

| Grip | Speed | Chance to Damage | Accuracy |
|---|---|---|---|
| Plain | 0.00 | 1.00 | 0.00 |
| Twine | 0.00 | 0.95 | +0.10 |
| Cloth | +0.10 | 0.90 | +0.20 |
| Leather | +0.20 | 0.80 | +0.30 |
| Sturdy | +0.30 | 0.65 | +0.40 |

**Chance to Damage** is a multiplier on whether the handle takes a hit at all, so **lower is
better**. A treatment multiplies this further, and the two stack multiplicatively — each
removes a share of what still gets through, so the pair can never reach a guaranteed save.

**Accuracy** is a bow stat, how much sway the bow has and how fast the reticle locks in on your view

### Bindings

| Binding | Base HP | Self HP | Handle HP | Recovery | Metal |
|---|---|---|---|---|---|
| None | 0.50 | 0.00 | 0.00 | 1.00 | |
| Reeds | 1.00 | 0.00 | 0.00 | 1.00 | |
| Twine | 1.20 | +0.10 | +0.05 | 0.90 | |
| Hide | 1.25 | +0.15 | +0.05 | 0.80 | |
| Rope | 1.25 | +0.15 | +0.05 | 0.70 | |
| Cloth | 1.30 | +0.20 | +0.10 | 0.75 | |
| Copper Nails | 1.40 | +0.10 | +0.10 | 0.90 | ✓ |
| Leather | 1.50 | +0.30 | +0.10 | 0.60 | |
| Tin Bronze Nails | 1.70 | +0.20 | +0.20 | 0.50 | ✓ |
| Bismuth Bronze Nails | 1.70 | +0.25 | +0.20 | 0.50 | ✓ |
| Black Bronze Nails | 1.70 | +0.30 | +0.20 | 0.50 | ✓ |
| Cupronickel Nails | 1.70 | +0.20 | +0.20 | 0.50 | ✓ |
| Sturdy | 1.80 | +0.30 | +0.25 | 0.55 | |
| Iron Nails | 1.80 | +0.30 | +0.20 | 0.45 | ✓ |
| Meteoric Iron Nails | 1.90 | +0.50 | +0.25 | 0.45 | ✓ |
| Glue | 2.00 | +0.30 | +0.30 | 1.00 | |
| Steel Nails | 2.20 | +0.60 | +0.40 | 0.35 | ✓ |

- **Base HP** and **Self HP** set the binding's own durability.
- **Handle HP** is what the binding adds to the handle it holds.
- **Recovery** is the salvage threshold: if the binding's HP falls below this fraction, the
  binding is ruined when another part breaks. **Lower is better** — steel nails survive down to
  35%, while reeds and glue are lost as soon as they take any wear.
- **Metal** bindings are nailed on, and are the only ones affected by the handle material's
  Nail Binding value.

Glue is the strongest non-metal binding by raw durability, but its `1.00` recovery means it is
never salvageable.


## Weapons



### Bows




## Credits and License

Haft is forked from [ToolSmith](https://github.com/Mario90900/Toolsmith) by Jon,
which was released under [CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/)
(a public-domain dedication). A large portion of the original code has since been
reworked or rewritten.

Haft is licensed under the [MIT License](LICENSE.txt).
