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

import re
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
#Lower than the 7 this used at one point; the cost of the difference is ebony
#stepping every ~190ms instead of ~128ms, which is not visible in play.
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
            lerp_element(c, kids_b[c["name"]], t) if c["name"] in kids_b else collapse(c, t)
            for c in ea["children"]
        ]

    return out


def collapse(element, t):
    """An element the later stage does not have, t of the way to gone.

    Vanilla changes the string's topology mid-draw rather than only its pose:
    charge1 models a relaxed string as two segments per limb (BowStringUp2 and
    BowStringDown2 alongside BowStringUp1/Down1) and charge2 drops to one. Copying
    such an element through unchanged, which is what this used to do, froze it at
    its charge1 pose for poses 0-4 and then deleted it outright at pose 5, so the
    string snapped once instead of drawing back.

    Shrinking it toward its own origin instead keeps the element present at every
    pose and lets it disappear continuously. That also keeps the element COUNT
    constant across the whole draw, so every pose is the same shape differently
    posed rather than a different shape.
    """
    out = dict(element)

    #t is how far toward GONE, so it runs from the element's own size at t=0 to a point
    #at t=1. lerp goes from `to` back to `from`, not the other way round.
    out["to"] = [round(lerp(b, a, t), 4) for a, b in zip(element["from"], element["to"])]

    if "children" in element:
        out["children"] = [collapse(c, t) for c in element["children"]]

    return out


def carry_missing(reference, shape):
    """`shape` with every element `reference` has and it lacks, added back collapsed.

    The added elements keep the reference's transform and draw nothing, so they occupy
    no space and change nothing visually - they exist so that the element set is the
    same in every stage and an element can therefore be interpolated across the whole
    sequence rather than appearing and disappearing between stages.
    """
    out = dict(shape)
    by_name = {e["name"]: e for e in shape.get("elements", [])}

    out["elements"] = [
        carry_missing_element(r, by_name[r["name"]]) if r["name"] in by_name else collapse(r, 1.0)
        for r in reference.get("elements", [])
    ]

    return out


def carry_missing_element(reference, element):
    """One element of carry_missing, recursing so nested children are carried too."""
    out = dict(element)
    ref_kids = reference.get("children", [])

    if ref_kids:
        by_name = {c["name"]: c for c in element.get("children", [])}
        out["children"] = [
            carry_missing_element(r, by_name[r["name"]]) if r["name"] in by_name else collapse(r, 1.0)
            for r in ref_kids
        ]

    return out


def tween(shape_a, shape_b, t):
    out = json.loads(json.dumps(shape_a))
    by_name_b = {e["name"]: e for e in shape_b["elements"]}
    out["elements"] = [
        lerp_element(e, by_name_b[e["name"]], t) if e["name"] in by_name_b else collapse(e, t)
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
        #The vanilla stages are normalised to the first stage's element set before any of
        #this. charge1 carries 26 elements and charge2/charge3 only 24 - the two the later
        #stages drop are the second string segment on each limb - so re-emitting them as
        #they ship would leave the pose sequence changing topology partway through however
        #carefully the tweens between them are built. Carrying the missing elements forward
        #collapsed keeps one element set across all 11 poses, which is what lets the string
        #shrink smoothly rather than vanish at a step.
        stages = [stages[0]] + [carry_missing(stages[0], s) for s in stages[1:]]

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

    check_pose_count_agrees(len(written) // 4)


def check_pose_count_agrees(poses):
    """The pose count lives in three places and they have drifted apart before.

    This script writes the pose shapes, bow-draw-tweens.json declares one shape
    alternate per pose, and BowStatPatches.DrawPoseCount decides which pose the code
    asks for. When they disagree the bow selects a shape that does not exist and
    renders the missing-asset placeholder, a long way from the edit that caused it.

    Counting is not enough on its own, which is the second check here. The count
    agreed everywhere while the code still scaled and clamped its pose to
    DrawPoseCount rather than to the highest INDEX, so it asked for draw11 out of
    draw0..draw10 at the top of every draw. Check what the code actually asks for,
    not only how many files there are.
    """
    problems = []

    patch = "Haft/assets/haft/patches/bow-draw-tweens.json"
    if os.path.isfile(patch):
        declared = len(json.load(open(patch))[0]["value"]["alternates"])
        if declared != poses:
            problems.append(f"{patch} declares {declared} alternates, generator wrote {poses} poses")

    src = "Haft/ToolTinkering/BowStatPatches.cs"
    if os.path.isfile(src):
        text = open(src).read()
        m = re.search(r"DrawPoseCount\s*=\s*(\d+)", text)
        if m and int(m.group(1)) != poses:
            problems.append(f"{src} DrawPoseCount is {m.group(1)}, generator wrote {poses} poses")

        for expr in re.findall(r"\*\s*BowStatHelper\.(DrawPose\w+)", text):
            if expr != "DrawPoseMaxIndex":
                problems.append(f"{src} scales a pose by {expr}; it must scale by DrawPoseMaxIndex")
        for expr in re.findall(r"Clamp\(pose,\s*0,\s*BowStatHelper\.(DrawPose\w+)\)", text):
            if expr != "DrawPoseMaxIndex":
                problems.append(f"{src} clamps a pose to {expr}; it must clamp to DrawPoseMaxIndex")

    for p in problems:
        print(f"  MISMATCH: {p}")
    if problems:
        sys.exit("pose count disagrees across files - fix before building")
    print("  pose count agrees across generator, patch and code")


if __name__ == "__main__":
    main()
