using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Toolsmith.Utils;
using Vintagestory.GameContent;

namespace Toolsmith.ToolTinkering.Blocks {
    public class BlockEntityGrindstone : BlockEntity {

        public bool grinding { get; set; }
        protected ILoadedSound grindingWheel;
        protected ILoadedSound honingScrape;
        string rotation;

        public virtual string Rotation {
            get { return rotation; }
            set {
                rotation = value;
                switch (value) {
                    case "north":
                        rotationVec.Y = 0;
                        break;
                    case "east":
                        rotationVec.Y = 270;
                        break;
                    case "west":
                        rotationVec.Y = 90;
                        break;
                    default:
                        rotationVec.Y = 180;
                        break;
                }
            }
        }
        public Vec3f rotationVec = new Vec3f();

        //Both looping sounds play from the middle of the block rather than its corner.
        private Vec3f SoundPos => Pos.ToVec3f().Add(0.5f, 0.5f, 0.5f);
        private bool isClient = false;

        BlockEntityAnimationUtil AnimUtil {
            get {
                return GetBehavior<BEBehaviorAnimatable>()?.animUtil;
            }
        }

        public override void Initialize(ICoreAPI api) {
            base.Initialize(api);

            isClient = api.Side == EnumAppSide.Client;
            Rotation = api.World.BlockAccessor.GetBlock(Pos).LastCodePart();

            if (isClient) {
                AnimUtil.InitializeAnimator("grindstone", null, null, rotationVec);
            }
            if (isClient && grinding) {
                OnBlockInteractStart();
            }
        }

        public void OnBlockInteractStart() {
            if (!isClient) {
                return;
            }

            AnimUtil?.StartAnimation(new AnimationMetaData() { Animation = "spinwheel", Code = "spinwheel", EaseInSpeed = 10, EaseOutSpeed = 2 });
            grinding = true;
            ToggleWheelSound(true);
            MarkDirty();
        }

        public void OnBlockInteractStop() {
            AnimUtil?.StopAnimation("spinwheel");
            grinding = false;
            ToggleWheelSound(false);
            MarkDirty();
        }

        //Modeled after the Rift Ward's sounds, and toggled on when starting interacting with the grindstone and off when canceled or stopped.
        private void ToggleWheelSound(bool startSound) {
            SoundUtility.ToggleLoopingSound(ref grindingWheel, startSound, Api, new AssetLocation("sounds/block/quern.ogg"), SoundPos, 1.0f, 1f, 1.0f);
        }

        public void ToggleHoningSound(bool startSound) {
            SoundUtility.ToggleLoopingSound(ref honingScrape, startSound, Api, new AssetLocation("toolsmith:sounds/grindstone-scraping-loop.ogg"), SoundPos, 0.75f, 1f, 0.2f);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve) {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            grinding = tree.GetBool("grinding");
            Rotation = tree.GetString("rotation", Rotation);

            if (grinding) {
                OnBlockInteractStart();
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree) {
            base.ToTreeAttributes(tree);

            tree.SetBool("grinding", grinding);
            tree.SetString("rotation", Rotation);
        }
    }
}
