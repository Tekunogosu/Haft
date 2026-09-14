using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Haft.Client {
    public interface IModularPartRenderer {

        public abstract ITreeAttribute InitializeRenderTree(ITreeAttribute tree, Item item);

        public abstract void ResetRotationAndOffset(ItemStack stack);
    }
}
