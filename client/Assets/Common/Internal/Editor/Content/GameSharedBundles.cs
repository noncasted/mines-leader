using System.Collections.Generic;

namespace Internal
{
    // The shared content bundles of the game (ContentSharedSync of the framework writes them into this folder): an asset two
    // bundles need directly goes to the shared bundle of the earliest phase among them - SharedCommon when the main bundle
    // needs it (preloaded with it), SharedMenu when the menu, the match or its views do.
    public sealed class GameSharedBundles : IContentSharedBundles
    {
        public string Folder => "Assets/Common/Internal/Editor/Content";

        public IReadOnlyList<ContentSharedBundle> Bundles { get; } = new[]
        {
            new ContentSharedBundle("SharedCommon", isPreloaded: true, isGzip: false, "Main"),
            new ContentSharedBundle("SharedMenu", isPreloaded: false, isGzip: true, "Menu", "GamePlayViews", "GamePlay")
        };
    }
}
