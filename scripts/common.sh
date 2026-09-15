# Shared settings for the testbed scripts. Sourced, never run directly.
#
# The testbed lives off the NVMe, alongside the other Vintage Story testbeds on
# this machine rather than on its own. Both overridable, so a machine that keeps
# things elsewhere is not forced into this layout.

TESTBED="${HAFT_TESTBED:-/mnt/media/testbed/haft}"
GAME="${VINTAGE_STORY:-$HOME/.local/share/vintagestory}"

SERVER_DATA="$TESTBED/server"
CLIENT_DATA="$TESTBED/client"

# The real install, read from only, to seed the testbed client's settings once.
REAL_DATA="${VINTAGE_STORY_DATA:-$HOME/.config/VintagestoryData}"

# The testbed's port. The default 42420 is held by the real server, 42450 by the
# Underrealm testbed and 42451 by Sated's; a testbed sharing any of them dies at
# socket bind with "Address already in use" -- after a startup log long enough to
# look like it got further than it did. Defined here rather than in either script
# so the server that binds it and the client that dials it cannot drift apart.
#
# Takes effect for the server only when serverconfig.json is written, which is
# on first run or after wipe-all. An existing testbed carries its old port until
# then, so testbed-server.sh corrects the file in place on every start.
TESTBED_PORT="${HAFT_PORT:-42470}"

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

die() { printf 'error: %s\n' "$1" >&2; exit 1; }

require_game() {
	[ -f "$GAME/VintagestoryServer.dll" ] ||
		die "Vintage Story not found at '$GAME'. Set VINTAGE_STORY to the folder holding VintagestoryServer.dll."
}

# The packaged zip's name carries BOTH the modid and the version from
# modinfo.json, so it moves on a version bump and on a rename. Read both rather
# than hardcoding either, or the next change leaves the scripts copying a file
# that is no longer produced.
mod_id() {
	python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["modid"])' \
		"$REPO/Haft/modinfo.json" || die "could not read the modid from Haft/modinfo.json"
}

mod_zip() {
	version=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["version"])' \
		"$REPO/Haft/modinfo.json") || die "could not read the version from Haft/modinfo.json"
	printf '%s/Releases/%s_%s.zip' "$REPO" "$(mod_id)" "$version"
}

# Mods installed alongside ours in the testbed, by filename, taken from the real
# install. These are the ones Haft actually has to keep working with:
# PrimitiveSurvival declares tool: "spear" on its fishing spears and is why the
# spear work needed a blacklist entry, SmithingPlusPlus is referenced by the
# reforging path, and Butchery supplies the bone tool heads.
#
# SmithingPlusPlus is a maintained fork of SmithingPlus. Haft's compat resolves
# either one, but the fork is what gets tested, because it is what is actually
# being run. It also absorbs the old smithingplusmaterialcache mod, which is why
# that is no longer listed separately.
TESTBED_EXTRA_MODS="${HAFT_EXTRA_MODS:-primitivesurvival_5.1.3.zip smithingplusplus_1.10.3.zip butchering_1.14.3.zip}"

# Copies the extra mods in if they are not already there. Kept separate from
# install_mod so a wipe restores them without a rebuild being involved.
install_extra_mods() {
	target="$1"
	mkdir -p "$target/Mods"

	for name in $TESTBED_EXTRA_MODS; do
		[ -f "$target/Mods/$name" ] && continue
		if [ -f "$REAL_DATA/Mods/$name" ]; then
			cp "$REAL_DATA/Mods/$name" "$target/Mods/$name"
			printf 'extra:  %s\n' "$name"
		else
			printf 'extra:  %s not found in %s/Mods, skipping\n' "$name" "$REAL_DATA"
		fi
	done
}

# Rebuilds the mod and drops it into one data dir's Mods folder. Both scripts do
# this on every launch so there is no separate build step to forget.
#
# build.sh runs the Cake build, which validates the JSON assets, publishes, and
# packages Releases/<modid>_<version>.zip -- the same zip a player installs.
# Testing that artifact rather than an unpacked bin/ folder is the point: an
# asset missing from the package is invisible until something loads the zip.
#
# Calling this once per target rebuilds once per target, which is wasteful but
# not wrong: the package is reproducible, so every build of unchanged source
# lands the same bytes in every target. That is what makes comparing an
# installed copy against Releases/ by hash meaningful.
install_mod() {
	target="$1"
	mkdir -p "$target/Mods"

	( cd "$REPO" && ./build.sh >/dev/null ) ||
		die "mod build failed (run ./build.sh to see why)"

	zip=$(mod_zip)
	[ -f "$zip" ] || die "the build did not produce $zip"

	# Installed under the zip's own name rather than a fixed one. A stale copy
	# under the previous name would otherwise sit alongside this build and load
	# beside it, which is exactly what happens after a modid change.
	installed="$target/Mods/$(basename "$zip")"
	remove_stale_mod_copies "$target" "$(basename "$zip")"

	cp "$zip" "$installed" || die "could not copy the mod into $target/Mods"
	printf 'mod:    %s -> %s\n' "$(basename "$zip")" "$installed"
}

# Deletes any previously installed copy of OUR mod from the testbed, matching on
# the modid recorded inside each zip rather than on its filename, so a copy left
# behind by an older name or dropped in by a mod manager is caught too. The zip
# being installed now is skipped, and the other mods listed in
# TESTBED_EXTRA_MODS are never considered.
remove_stale_mod_copies() {
	target="$1"
	keep="$2"
	ours=$(mod_id)

	for existing in "$target"/Mods/*.zip; do
		[ -e "$existing" ] || continue
		[ "$(basename "$existing")" = "$keep" ] && continue

		existing_id=$(unzip -p "$existing" modinfo.json 2>/dev/null |
			python3 -c 'import sys,re; m=re.search(r"\"modid\"\s*:\s*\"([^\"]+)\"", sys.stdin.read(), re.I); print(m.group(1) if m else "")' 2>/dev/null)

		# Match the current modid, and also the name this mod shipped under
		# before the rename, so an old toolsmith_*.zip is cleared out.
		case "$existing_id" in
			"$ours"|toolsmith)
				rm -f "$existing" &&
					printf 'mod:    removed stale %s (modid %s)\n' "$(basename "$existing")" "$existing_id"
				;;
		esac
	done
}
