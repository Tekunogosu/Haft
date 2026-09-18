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

#The arrow hangs off this element on every tier. Where it hangs differs - it is a
#root-level sibling of origin on crude, long and recurve, and nested under BowGrip
#on simple - but the carrier machinery in strip_to_part already handles either, so
#the position never has to be special-cased.
#
#The arrow is selected as a SUBTREE rather than by listing element names, because
#its names are not stable across the four tiers the way the bow's are: the head's
#cubes are Cube17/18 on crude and simple, Cube23/24 on long and Cube25/26 on
#recurve, and crude has no fletching at all. Listing them would silently drop
#geometry the next time a tier numbered its cubes differently. This name is the
#one that IS stable, and everything under it is the arrow.
ARROW_PART = "arrow"
ARROW_ROOT = "Stick"

#The texture key each part's shape declares, and the fallback texture behind it.
#The keys match what the rest of the mod already uses: handles declare "wood",
#grips declare "grip", metal parts declare "material". Reusing them means a bow
#part is textured by the same code path as a tool part.
PART_TEXTURES = {
    "limb": ("wood", "game:block/wood/debarked/oak"),
    "grip": ("grip", "game:block/leather/plain"),
    "string": ("material", "game:item/resource/string"),
}

#The arrow is the one part that declares more than one texture key, because it is
#the one part made of more than one thing: a wooden shaft, a fletching, and a head
#of some metal or stone. Those are three axes an arrow system will want to set
#separately, so they are kept apart at the shape level now rather than being
#flattened into one key and having to be split again later.
#
#The keys are the mod's existing ones for the same materials - "wood" for a shaft
#the way a handle declares its wood, "material" for a head the way a metal part
#does - so an arrow part is textured by the code path that already exists.
ARROW_TEXTURES = {
    "wood": "game:block/wood/debarked/oak",
    "feather": "game:item/tool/feather",
    "material": "game:block/metal/ingot/blackbronze",
}

#Which of the arrow's three keys each element takes. Matched on the element's own
#name rather than on the texture the vanilla shape gave it, because the vanilla
#texture is exactly what differs per tier - flint against blackbronze against iron
#for the head, feather against feather2 for the fletching - while the names of the
#parts of an arrow do not.
#
#The head is everything under ArrowHead, including its numbered cubes, so it is
#matched as a subtree rather than by listing names that change per tier.
ARROW_HEAD_ROOT = "ArrowHead"
ARROW_FLETCHING_NAMES = {"FeatherVertical", "FeatherHorizontal", "featherV", "featherH"}


def arrow_texture_key(name, under_head):
    """The texture key an element of the arrow declares."""
    if under_head:
        return "material"
    if name in ARROW_FLETCHING_NAMES:
        return "feather"
    return "wood"


def strip_to_part(element, wanted, inside=False):
    """Returns this element for a part's shape, or None if nothing under it is wanted.

    An element that is not itself wanted but has a wanted descendant is kept as a
    carrier: its transform still applies, but it draws nothing.

    `wanted` is either a set of element names, or the name of a subtree root - see
    ARROW_ROOT for why the arrow is selected the second way. `inside` tracks
    descent through such a subtree, so every element under the root is drawn
    without each having to be named.

    Descent only carries for a subtree selection. With a name set, an element's
    children are each tested on their own name - the bow's parts are interleaved
    down one chain of elements, so BowStringUp1 hangs under BowTipUp and belongs
    to the string rather than to the limb it is attached to.
    """
    subtree = isinstance(wanted, str)
    mine = inside or (element["name"] == wanted if subtree else element["name"] in wanted)

    kids = [
        k for k in (strip_to_part(c, wanted, mine and subtree) for c in element.get("children", []))
        if k is not None
    ]

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
    arrow = part == ARROW_PART
    wanted = ARROW_ROOT if arrow else PART_ELEMENTS[part]

    roots = [
        r for r in (strip_to_part(e, wanted) for e in shape["elements"])
        if r is not None
    ]

    textures = dict(ARROW_TEXTURES) if arrow else {PART_TEXTURES[part][0]: PART_TEXTURES[part][1]}

    out = {
        "textureWidth": shape.get("textureWidth", 16),
        "textureHeight": shape.get("textureHeight", 16),
        "textures": textures,
        "elements": roots,
    }

    #textureSizes is deliberately NOT carried over. The original names a size per
    #texture key of the whole bow - string, aged, bone, leather and the arrow's own
    #keys - and a part shape declares exactly one key. Copying the block wholesale
    #leaves it describing keys the shape no longer has, which is what makes a shape
    #fail to load and render as the missing-asset placeholder.
    sizes = {
        k: shape.get("textureSizes", {}).get(k)
        for k in textures
        if shape.get("textureSizes", {}).get(k) is not None
    }
    if sizes:
        out["textureSizes"] = sizes

    return out


def retexture(element, key):
    """Points every drawn face at the part's single texture key."""
    for face in element.get("faces", {}).values():
        face["texture"] = "#" + key
    for child in element.get("children", []):
        retexture(child, key)


def retexture_arrow(element, under_head=False):
    """Points each of the arrow's elements at the key for what it actually is.

    Unlike the single-key parts, the arrow keeps its three materials apart - see
    ARROW_TEXTURES. The head is matched as a subtree so its numbered cubes, which
    differ per tier, follow their parent rather than needing to be named.
    """
    under_head = under_head or element["name"] == ARROW_HEAD_ROOT
    key = arrow_texture_key(element["name"], under_head)

    for face in element.get("faces", {}).values():
        face["texture"] = "#" + key
    for child in element.get("children", []):
        retexture_arrow(child, under_head)


def main():
    if not os.path.isdir(POSES):
        sys.exit(f"{POSES} not found - run gen-bow-charge-tweens.py first")

    poses = sorted(
        {f.rsplit("-draw", 1)[1][:-5] for f in os.listdir(POSES) if "-draw" in f},
        key=int,
    )
    if not poses:
        sys.exit(f"no -draw poses found in {POSES}")

    #The arrow is generated alongside the three bow parts rather than in a pass of
    #its own: the renderer asks for one shape per part per pose, and a part that
    #skipped a pose would send it looking for a file that is not there.
    parts = list(PART_ELEMENTS) + [ARROW_PART]

    written = 0
    for tier in TIERS:
        for part in parts:
            os.makedirs(f"{OUT}/{part}", exist_ok=True)

        for pose in poses:
            src = f"{POSES}/{tier}-draw{pose}.json"
            if not os.path.isfile(src):
                sys.exit(f"missing {src}")
            shape = json.load(open(src))

            for part in parts:
                built = build_part(shape, part)
                if part == ARROW_PART:
                    for e in built["elements"]:
                        retexture_arrow(e)
                else:
                    for e in built["elements"]:
                        retexture(e, PART_TEXTURES[part][0])

                path = f"{OUT}/{part}/{tier}-draw{pose}.json"
                with open(path, "w") as fh:
                    json.dump(built, fh, indent="\t")
                    fh.write("\n")
                written += 1

        print(f"{tier}: {len(poses)} poses x {len(parts)} parts")

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
