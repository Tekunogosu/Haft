using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Haft.Utils {

    //The looping sounds this mod plays - a grindstone wheel turning, an edge being drawn across stone - are all
    //started, faded and disposed the same way. Only the asset, the volume and where the sound sits differ.
    public static class SoundUtility {

        //Starts the sound if it is not already playing, or fades a playing one back up to volume. Passing false fades
        //it out and disposes it, clearing the reference so the next start builds a fresh sound.
        //
        //Clientside only: LoadSound needs an IClientWorldAccessor, and a sound has nothing to do on a server.
        public static void ToggleLoopingSound(ref ILoadedSound sound, bool startSound, ICoreAPI api, AssetLocation location, Vec3f position, float volume, float fadeInSeconds, float fadeOutSeconds) {
            if (!startSound) {
                sound?.FadeOut(fadeOutSeconds, (s) => { s.Dispose(); });
                sound = null;
                return;
            }

            if (api == null || !api.Side.IsClient()) {
                return;
            }

            if (sound == null || !sound.IsPlaying) {
                sound = ((IClientWorldAccessor)api.World).LoadSound(new SoundParams() {
                    Location = location,
                    ShouldLoop = true,
                    Position = position,
                    DisposeOnFinish = false,
                    Volume = 0,
                    Range = 6,
                    SoundType = EnumSoundType.Ambient
                });

                sound?.Start();
            }

            sound?.FadeTo(volume, fadeInSeconds, (s) => { });
        }
    }
}
