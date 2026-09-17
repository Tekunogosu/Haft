#!/usr/bin/env python3
"""Generates intermediate bow charge shapes by interpolating vanilla's four.

Vanilla draws a bow by swapping between four discrete shape files - base,
charge1, charge2, charge3 - selected by the renderVariant attribute. There are
no frames between them, so the draw visibly steps rather than bends. Once a
heavy limb stretches the draw past a second, those steps are far enough apart
to read as a snap.

The four shapes differ almost entirely by rotation on identically-named
elements (limbs bending, string drawing back, tips flexing), so the in-between
poses can be produced by interpolating those numbers. This writes them out as
additional shape files, which are then declared as extra shape alternates so
the renderer steps through a longer sequence.

Only charge1..charge3 are interpolated. The base shape holds 17 elements while
the charge shapes hold 24 - the extra 7 are the nocked arrow, which appears the
moment the bow is drawn - so base->charge1 is a genuine geometry change rather
than a pose change and is left as the discrete step it already is.

Run from the repo root. Rewrites its output directory each time.
"""

import json
import os
import sys

VANILLA = os.environ.get(
    "VINTAGE_STORY", os.path.expanduser("~/.local/share/vintagestory")
) + "/assets/survival/shapes/item/tool/bow"
OUT = "Haft/assets/haft/shapes/item/tool/bow"

#How many generated poses sit between each pair of vanilla stages. 4 turns the
#three vanilla charge stages into 11.
#
#Lower than the 7 this used before parts existed. Splitting each pose into three
#part shapes triples the file count, so a pose now costs three files rather than
#one: 11 poses is 132 part files where 17 would be 204. The cost is ebony
#stepping every ~190ms instead of ~128ms.
#
#The count decides how far the bow jumps at each step, and it matters most on a slow
#limb. Going much further gives less than it looks like: past this the limit becomes
#the timing curve holding the first pose rather than the number of poses available.
STEPS_BETWEEN = 4

#The element fields that vary between charge stages and can be interpolated.
#Everything else (name, faces, uv, shade) is copied from the earlier stage.
LERP_VECTORS = ("from", "to", "rotationOrigin")
LERP_SCALARS = ("rotationX", "rotationY", "rotationZ")


def lerp(a, b, t):
    return a + (b - a) * t


def lerp_element(ea, eb, t):
    """One element's pose, t of the way from ea to eb. Structure follows ea."""
    out = dict(ea)

    for field in LERP_VECTORS:
        if field in ea and field in eb:
            out[field] = [round(lerp(x, y, t), 4) for x, y in zip(ea[field], eb[field])]

    for field in LERP_SCALARS:
        if field in ea or field in eb:
            va, vb = ea.get(field, 0.0), eb.get(field, 0.0)
            out[field] = round(lerp(va, vb, t), 4)

    if "children" in ea:
        kids_b = {c["name"]: c for c in eb.get("children", [])}
        out["children"] = [
            lerp_element(c, kids_b[c["name"]], t) if c["name"] in kids_b else dict(c)
            for c in ea["children"]
        ]

    return out


def tween(shape_a, shape_b, t):
    out = json.loads(json.dumps(shape_a))
    by_name_b = {e["name"]: e for e in shape_b["elements"]}
    out["elements"] = [
        lerp_element(e, by_name_b[e["name"]], t) if e["name"] in by_name_b else e
        for e in shape_a["elements"]
    ]
    return out


def main():
    if not os.path.isdir(VANILLA):
        sys.exit(f"vanilla bow shapes not found at {VANILLA}")

    os.makedirs(OUT, exist_ok=True)
    written = []

    for tier in ("crude", "simple", "long", "recurve"):
        stages = []
        for n in ("charge1", "charge2", "charge3"):
            path = f"{VANILLA}/{tier}-{n}.json"
            if not os.path.isfile(path):
                sys.exit(f"missing {path}")
            stages.append(json.load(open(path)))

        #Sequence: charge1, tweens, charge2, tweens, charge3. The vanilla stages
        #are re-emitted too so the whole run is one contiguous set of files and
        #the item JSON can list them in order without mixing domains.
        seq = []
        for i in range(len(stages) - 1):
            seq.append(stages[i])
            for s in range(1, STEPS_BETWEEN + 1):
                seq.append(tween(stages[i], stages[i + 1], s / (STEPS_BETWEEN + 1)))
        seq.append(stages[-1])

        for i, shape in enumerate(seq):
            name = f"{tier}-draw{i}"
            with open(f"{OUT}/{name}.json", "w") as fh:
                json.dump(shape, fh, indent="\t")
                fh.write("\n")
            written.append(name)

        print(f"{tier}: {len(seq)} poses ({len(stages)} vanilla + {len(seq)-len(stages)} generated)")

    print(f"\nwrote {len(written)} files to {OUT}")


if __name__ == "__main__":
    main()
