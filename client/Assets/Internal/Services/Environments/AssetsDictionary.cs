using System;
using System.Collections.Generic;

namespace Internal
{
    [Serializable]
    public class AssetsDictionary : SerializableDictionary<string, List<EnvAsset>>
    {
    }
}