#!/usr/bin/env python3
"""Splits the bow draw poses into independently textured parts.

Vanilla draws a bow as one mesh, so the whole thing takes a single set of
textures and nothing can be coloured per material. Haft renders tools as a
composed mesh instead - ModularPartRenderingFromAttributes walks a multi-part
render tree, tesselates each part's own shape with its own textures, and adds
the results together - so a bow only needs its shape splitting into parts for
the same system to drive it.

Three parts, matching how a bow is actually built:

  limb    the two limbs, their tips, notches and composite layers   texture key "wood"
  grip    the riser the hand holds                                  texture key "grip"
  string  the string segments and the nock                          texture key "material"

The nocked arrow (Stick and its children) is deliberately in NO part. It is not
part of the bow, and separating it also settles the texture collision where the
arrow shaft shares a key with the limbs on three of the four tiers.

Element coordinates are relative to the parent and rotations compound down the
chain, so a part cannot simply be lifted to the root - BowStringUp1 sits under
BowTipUp under BowLimbUp under BowGrip, and inherits three rotations on the way.
Rather than flatten that by hand, each part keeps its ancestors as zero-size
carrier elements with their transforms intact and only the wanted elements
drawn. The engine then applies exactly the transform chain it always did.

Run from the repo root, after gen-bow-charge-tweens.py.
"""

import json
import os
import re
import sys

POSES = "Haft/assets/haft/shapes/item/tool/bow"
OUT = "Haft/assets/haft/shapes/item/parts/bow"

TIERS = ("crude", "simple", "long", "recurve")

#Which leaf elements belong to which part, by name. Anything not listed, and
#anything under Stick, is dropped from every part.
PART_ELEMENTS = {
    "limb": {
        "BowLimbUp", "BowLimbDown",
        "BowTipUp", "BowTipDown",
        "BowNotchUp", "BowNotchDown",
        "BowCompositeUp1", "BowCompositeUp2",
        "BowCompositeDown1", "BowCompositeDown2",
    },
    "grip": {"BowGrip"},
    "string": {
        "BowStringUp1", "BowStringUp2",
        "BowStringDown1", "BowStringDown2",
        "BowNock",
    },
}

#The texture key each part's shape declares, and the fallback texture behind it.
#The keys match what the rest of the mod already uses: handles declare "wood",
#grips declare "grip", metal parts declare "material". Reusing them means a bow
#part is textured by the same code path as a tool part.
PART_TEXTURES = {
    "limb": ("wood", "game:block/wood/debarked/oak"),
    "grip": ("grip", "game:block/leather/plain"),
    "string": ("material", "game:item/resource/string"),
}


def strip_to_part(element, wanted, ancestors_kept=False):
    """Returns this element for a part's shape, or None if nothing under it is wanted.

    An element that is not itself wanted but has a wanted descendant is kept as a
    carrier: its transform still applies, but it draws nothing.
    """
    kids = [
        k for k in (strip_to_part(c, wanted) for c in element.get("children", []))
        if k is not None
    ]
    mine = element["name"] in wanted

    if not mine and not kids:
        return None

    out = dict(element)
    if kids:
        out["children"] = kids
    else:
        out.pop("children", None)

    if not mine:
        #A carrier: keep the transform, draw nothing. Collapsing it to a point
        #rather than deleting its faces keeps the element valid for the
        #tesselator while contributing no geometry.
        out["to"] = list(out["from"])
        out.pop("faces", None)

    return out


def build_part(shape, part):
    wanted = PART_ELEMENTS[part]
    key, fallback = PART_TEXTURES[part]

    roots = [
        r for r in (strip_to_part(e, wanted) for e in shape["elements"])
        if r is not None
    ]

    out = {
        "textureWidth": shape.get("textureWidth", 16),
        "textureHeight": shape.get("textureHeight", 16),
        "textures": {key: fallback},
        "elements": roots,
    }

    #textureSizes is deliberately NOT carried over. The original names a size per
    #texture key of the whole bow - string, aged, bone, leather and the arrow's own
    #keys - and a part shape declares exactly one key. Copying the block wholesale
    #leaves it describing keys the shape no longer has, which is what makes a shape
    #fail to load and render as the missing-asset placeholder.
    size = shape.get("textureSizes", {}).get(key)
    if size is not None:
        out["textureSizes"] = {key: size}

    return out


def retexture(element, key):
    """Points every drawn face at the part's single texture key."""
    for face in element.get("faces", {}).values():
        face["texture"] = "#" + key
    for child in element.get("children", []):
        retexture(child, key)


def main():
    if not os.path.isdir(POSES):
        sys.exit(f"{POSES} not found - run gen-bow-charge-tweens.py first")

    poses = sorted(
        {f.rsplit("-draw", 1)[1][:-5] for f in os.listdir(POSES) if "-draw" in f},
        key=int,
    )
    if not poses:
        sys.exit(f"no -draw poses found in {POSES}")

    written = 0
    for tier in TIERS:
        for part in PART_ELEMENTS:
            os.makedirs(f"{OUT}/{part}", exist_ok=True)

        for pose in poses:
            src = f"{POSES}/{tier}-draw{pose}.json"
            if not os.path.isfile(src):
                sys.exit(f"missing {src}")
            shape = json.load(open(src))

            for part in PART_ELEMENTS:
                built = build_part(shape, part)
                key = PART_TEXTURES[part][0]
                for e in built["elements"]:
                    retexture(e, key)

                path = f"{OUT}/{part}/{tier}-draw{pose}.json"
                with open(path, "w") as fh:
                    json.dump(built, fh, indent="\t")
                    fh.write("\n")
                written += 1

        print(f"{tier}: {len(poses)} poses x {len(PART_ELEMENTS)} parts")

    print(f"\nwrote {written} files to {OUT}")
    check_pose_count_agrees(len(poses))


def check_pose_count_agrees(poses):
    """The pose count lives in three places and they have drifted apart before.

    The generator writes the files, bow-draw-tweens.json declares one shape alternate
    per pose, and BowStatPatches.DrawPoseCount decides which pose the code asks for.
    When they disagree the bow selects a shape that does not exist and renders the
    missing-asset placeholder, which is a long way from the edit that caused it.
    """
    problems = []

    patch = "Haft/assets/haft/patches/bow-draw-tweens.json"
    if os.path.isfile(patch):
        declared = len(json.load(open(patch))[0]["value"]["alternates"])
        if declared != poses:
            problems.append(f"{patch} declares {declared} alternates, generator wrote {poses} poses")

    src = "Haft/ToolTinkering/BowStatPatches.cs"
    if os.path.isfile(src):
        m = re.search(r"DrawPoseCount\s*=\s*(\d+)", open(src).read())
        if m and int(m.group(1)) != poses:
            problems.append(f"{src} DrawPoseCount is {m.group(1)}, generator wrote {poses} poses")

    for p in problems:
        print(f"  MISMATCH: {p}")
    if problems:
        sys.exit("pose count disagrees across files - fix before building")
    print("  pose count agrees across generator, patch and code")


if __name__ == "__main__":
    main()
