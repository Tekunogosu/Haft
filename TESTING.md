# Toolsmith — testing checklist

Build `5e833f3d`, installed to both testbed dirs at
`/mnt/media/testbed/toolsmith/{server,client}/Mods/toolsmith.zip`.

Nothing in this list has been verified in play. Items are ordered so that a
failure early on explains failures later — if section 1 fails, most of the rest
will too, so stop and report rather than working down the list.

Mark each as pass or fail. For a failure, the most useful thing to capture is the
server log line if there is one, and the exact tooltip text if there is not.

---

## 1. Regression — the existing game still works

The riskiest change this session was durability decoupling, which touched every
tool rather than only metal ones.

- [ ] **A wooden handle still crafts.** Knife plus sandpaper plus a supportbeam,
      as before. Should name its wood: `Carved Tool Handle (Oak)`.
- [ ] **Its tooltip reads `Handle: Oak`** — the label changed from `Material:`
      this session. `Density: x1.00` below it for oak.
- [ ] **A tool built from it works** and shows the handle's wood in its own
      tooltip.
- [x] **An existing tool from before this session still works.** VERIFIED on the
      live server, 2026-09-13. This was the migration path: the attribute was
      renamed, and old handles are rewritten the first time anything reads them.
- [ ] **Grips still attach** to wooden handles, all types.
- [ ] **Wood treatments still work**: fat, beeswax, oil on a carved or
      professional handle.

## 2. Durability — the numbers should have changed

Handle durability no longer scales off the tool head. The same handle should now
be identical on every tool.

- [ ] **Steel pickaxe with a copper metal handle**: handle durability ≈ **2990**.
      Before this change it was ~12000. This is the bug that prompted the work.
- [ ] **The same copper handle on a stone axe**: also ≈ **2990**. Identical to the
      line above — that is the whole point.
- [ ] **Iron metal handle** ≈ **6900**. **Steel** ≈ **10465**.
- [ ] **Professional oak handle** ≈ **1650**. **Carved oak** ≈ **1260**.
      **Stick** ≈ **600**.
- [ ] **Bindings also decoupled** — steel nails should read the same regardless of
      what head the tool has.

Small deviations are fine; a binding or treatment bonus will move these. An
order-of-magnitude difference is not.

## 3. The metal handle, smithed

- [ ] **The recipe appears** when a metal bar is placed on an anvil. If it does
      not, nothing below this can be tested.
- [ ] **It costs about one bar.** Pattern is 46 voxels over two layers.
- [ ] **The handle names its metal**: `Metal Tool Handle (Iron)`, tooltip
      `Handle: Iron`, `Density: x3.00`.
- [ ] **Several metals work** — try copper, iron and steel at least. 18 are
      allowed.
- [ ] **Two metals that have no plate texture**, cupronickel especially, should
      still render rather than showing a missing texture. They fall back to the
      ingot texture.
- [ ] **A tool built from a metal handle** shows `Handle: Iron` in its tooltip.

## 4. Treatments — the tag gate

The point of the tag system is that a treatment only applies to a material that
suits it. A refusal produces nothing in the output slot; that is the intended
behaviour and matches how the mod already refuses an already-treated handle.

- [ ] **Metal handle plus fat** → `Greased`. Grease works on both materials.
- [ ] **Metal handle plus beeswax** → refuses, no output.
- [ ] **Metal handle plus oil** → refuses, no output.
- [ ] **Wood handle plus fat** → still works, unchanged.
- [ ] **Wood handle plus beeswax or oil** → still works, unchanged.
- [ ] **Tooltip shows a `Wear Save:` line** under `Treatment:` on any treated
      handle. Greased should read 4%.

## 5. Bluing — the forge interaction

The least certain part of this session. Three private fields of the forge are
read reflectively and any of them could be named differently in this game
version.

- [ ] **Cold metal handle into a lit forge with fuel.** Once it reaches roughly
      300C it should become `Blued`: `Treatment: +55%`, `Wear Save: 15%`.
- [ ] **THE EXPLOIT CASE.** Smith a handle and put it straight into a lit forge
      while it is still hot from the anvil. It must **not** blue. It passes down
      through 300C on its way to cooling, and blueing there would mean receiving
      the finish rather than performing it.
- [ ] **After that handle cools** — below 100C, in your inventory is fine — put it
      back in a lit forge. It **should** blue this time.
- [ ] **A cold forge, or one with no fuel**, should not blue anything however hot
      the handle is.
- [ ] **A wooden handle in a forge** should not blue. It is not metal.

If blueing never happens, check the server log for:

    Could not find the 'contents' field on BlockEntityForge

That message means the field was renamed and names the cause. **If blueing fails
and the log is silent, the other two fields are the suspects** — `burning` and
`fuelLevel` fail quietly, which is the weak point of the current implementation.

## 6. Adhesive grips

A grip backed with an adhesive is the SAME item carrying an attribute, not a new
variant. A metal handle refuses a plain grip and accepts a backed one.

- [ ] **Back a grip with glue.** Any grip plus a container of pitch glue in the
      grid. The output is the same grip, and its tooltip should gain a line:
      `Backed with ... - will hold on a smooth handle.`
- [ ] **Plain grip onto a metal handle** → refuses, no output.
- [ ] **Backed grip onto a metal handle** → attaches normally.
- [ ] **Plain grip onto a wooden handle** → still works, unchanged. Wooden handles
      require nothing.
- [ ] **Backed grip onto a wooden handle** → also works. Backing is never a
      penalty.
- [ ] **Butchery glues work too** if it is installed - sinew and hide glue are
      registered under the same binding stat tag as vanilla pitch glue.

## 7. Swing speed by material

Speed now comes from three places that add together: the handle tier, the
material, and the grip. Metals carry a weight penalty scaled by real density,
and meteoric iron is the one metal that swings FASTER than neutral.

- [ ] **Iron or steel metal handle**: `Use Speed: -15%`. Unchanged from before,
      since the penalty simply moved from the tier onto the material.
- [ ] **Meteoric iron metal handle**: `Use Speed: +5%`. This is the whole point -
      a metal that swings well, worth seeking over plain iron.
- [ ] **Gold metal handle**: `Use Speed: -37%`. Gold is nearly twice the density
      of iron and should feel awful.
- [ ] **Wooden handles unchanged**: professional still `+10%`, carved `+5%`.
      Wood species does not affect speed.
- [ ] **A grip still stacks on top.** Meteoric iron with a sturdy grip should
      read `+35%`.

## 8. Uranium compatibility

UraniumExpanded is installed in the testbed.

- [ ] **Uranium, ferrous uranium and uranium steel** appear as metal handle
      options on the anvil.
- [ ] **No duplicate-entry errors** in the server log at startup. Earlier in this
      session these appeared as `Attempted to add a MaterialStatDefine that
      already exists`; that was fixed and should not return.

## 9. Startup log

- [ ] **No Toolsmith errors** on server start.
- [ ] A line reporting that all **19 known handle materials** resolved to a stat
      block. If it lists missing ones instead, a stat table is not loading.

---

## Known gaps, not worth reporting

These are understood and deliberate:

- Blueing happens automatically once the conditions are met, with no way to
  decline it. Harmless while nothing else needs a hot handle.
- Fuel type is not checked. Any of the seven forge fuels blues, because the fuel
  is a heat source rather than a reagent.
- A handle's own tooltip reads `Handle: Oak` while sitting on a handle, which is
  mildly redundant. It shares a lang key with the tool tooltip, where the label is
  correct.
- The grid still offers treatment combinations that will be refused. Recipes are
  generated per handle part, and one part covers every wood, so the check can only
  run at craft time.
