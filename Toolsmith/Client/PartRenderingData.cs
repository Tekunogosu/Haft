using System;

namespace Toolsmith.Client {

    //The render defaults a part's item type declares in Json, read once at load. Anything that varies per stack -
    //which wood, which grip - lives in that stack's own attribute tree instead.
    public class PartData {
        public TextureData[] textures { get; set; } = Array.Empty<TextureData>();
        public bool skipCreativeInventoryAdditions = true;
        public string[] creativeTabs { get; set; } = Array.Empty<string>(); //What tabs should these items show up in?
    }

    public class TextureData {
        public string code { get; set; } = ""; //Code for the texture entry of the shape.
        public string Default { get; set; } = ""; //Default texture fallback.
        public string[] values { get; set; } = Array.Empty<string>(); //The different possible textures available.
    }
}
