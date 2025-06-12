using System.Collections.Generic;

namespace Internal
{
    public interface IAssetsStorage
    {
        IReadOnlyDictionary<string, IReadOnlyList<EnvAsset>> Assets { get; }
        IReadOnlyDictionary<PlatformType, OptionsRegistry> Options { get; }

        void Cache();
    }
}