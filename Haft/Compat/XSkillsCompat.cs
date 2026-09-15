using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using XLib.XLeveling;
using XSkills;

namespace Haft.Compat {

    //Everything Haft knows about XSkills' experience system. Bluing a handle is metalworking the player performed, so
    //it pays into the metalworking skill the same way finishing a piece on the anvil does.
    //
    //Callers check IsModEnabled("xskills") before calling in. The guard stays with them rather than moving in here,
    //for the same reason it does on the CAN Jewelry compat: a hidden second check reads as though the work were
    //unconditional.
    public static class XSkillsCompat {

        //Bluing pays half of what finishing a piece on the anvil pays. The anvil's award is expBase plus expPerHit for
        //every hit landed; bluing has no hits to count, so only the base half carries over. Read from the live config
        //rather than hardcoded, so a server that retunes metalworking retunes bluing with it.
        private const float BluingExperienceFraction = 0.5f;

        //How far from the forge a player can be and still be credited. BlockEntityForge records no owner - the piece
        //blues on a forge tick, not on a player action - so the nearest player is the best available answer. XSkills
        //itself resolves an unattributed quench the same way and with the same radius, in SafeQuenchingPatch.
        private const double CreditRadius = 15.0;

        public static void AwardBluingExperience(IWorldAccessor world, BlockPos pos) {
            if (world?.Api == null || world.Side != EnumAppSide.Server || pos == null) {
                return;
            }

            var metalworking = XLeveling.Instance(world.Api)?.GetSkill("metalworking") as Metalworking;
            if (metalworking == null) {
                return;
            }

            var center = new Vec3d(pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5);
            var player = world.NearestPlayer(center.X, center.Y, center.Z);
            if (player?.Entity == null || player.Entity.Pos.DistanceTo(center) > CreditRadius) {
                return;
            }

            var playerSkill = player.Entity.GetBehavior<PlayerSkillSet>()?[metalworking.Id];
            if (playerSkill == null) {
                return;
            }

            //A server may have replaced the config with one of a different shape; nothing to pay out if so.
            if (!(metalworking.Config is MetalworkingConfig config)) {
                return;
            }

            var experience = config.expBase * BluingExperienceFraction;
            if (experience <= 0.0f) {
                return;
            }

            playerSkill.AddExperience(experience);
        }
    }
}
