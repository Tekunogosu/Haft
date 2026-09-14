# Toolsmith.Tests

Unit tests for the parts of Toolsmith that can run without the game.

    dotnet test Toolsmith.Tests/Toolsmith.Tests.csproj

Needs `VINTAGE_STORY` set to the game install, same as the mod project - the tests reference the game
assemblies to compile against its types, but never start the game.

## What is covered, and what is not

A Vintage Story mod is mostly behaviours, patches and renderers that only mean anything with a world
loaded, and a test that drove those through stubs would be asserting against the stubs rather than the
game. So the suite deliberately covers the layer underneath: the arithmetic and the config loading, which
are ordinary code with no engine in them.

- `Config/HandleDurabilityTests`, `Config/BindingDurabilityTests` - the durability formulas. The expected
  figures are the ones `TESTING.md` asks a player to read off a tooltip, so a failure here names a number
  that changed in play.
- `Config/SpeedAndWearTests` - swing speed and the chance a handle takes damage, including the rules the
  two combine under: speed adds, wear multiplies.
- `Config/VerifyAndStoreDefinesTests` - loading defines out of the JSON configs, which is the path other
  mods' compat patches feed into. Covers the malformed entries it has to survive.
- `Utils/MathUtilityTests` - the wear, honing and reforging math.
- `Utils/TooltipColorTests`, `Utils/TooltipLineRemovalTests` - the tooltip banding thresholds and the
  buffer edit that takes a vanilla line back out.

Everything requiring a live world - rendering, block entities, the forge interaction behind blueing, the
Harmony patches - stays in `TESTING.md` as a manual checklist. Those are not untested by oversight; they
are tested by playing.

## Fixtures

`Support/StatFixtures` holds the stat blocks, transcribed from the shipped JSON rather than loaded from it.
That is deliberate: a test that read the config would still pass after a retune that changed the balance,
which is exactly the change worth being told about. When a stat is retuned on purpose, the fixture and the
expected figure both move, and the diff says what the retune did.

`Support/FakeWorld` and `Support/RecordingLogger` stand in for the two game types the code under test
reaches for. Both are `DispatchProxy` rather than hand-written stubs, because the interfaces run to 97 and
33 abstract members respectively while the mod touches one property and a few log calls. `FakeWorld` throws
on anything it does not model, so a test cannot quietly pass against a default value a real world would
have filled in.

`Support/ModStaticsFixture` sets `ToolsmithModSystem.Logger` and `.Stats`, which are mutable statics the
game assigns at load. Tests touching them join the `ModStatics` collection so they run serially, and the
scope restores the previous values on dispose.
