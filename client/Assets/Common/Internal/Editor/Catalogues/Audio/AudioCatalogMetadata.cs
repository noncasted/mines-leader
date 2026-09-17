using System;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Internal
{
    public sealed class AudioCatalogMetadata
    {
        public const float DefaultVolume = 1f;

        private const string UserDataKey = "audioCatalog";
        private const string LogTag = "AudioCatalogMetadata";

        public bool Included { get; set; }
        public string Group { get; set; } = string.Empty;
        public float Volume { get; set; } = DefaultVolume;

        public static bool TryRead(AssetImporter importer, out AudioCatalogMetadata metadata)
        {
            metadata = null;

            if (AssetUserData.TryRead(importer, UserDataKey, LogTag, out var catalog) == false)
                return false;

            metadata = new AudioCatalogMetadata
            {
                Included = catalog["included"] != null && catalog.Value<bool>("included"),
                Group = catalog.Value<string>("group") ?? string.Empty,
                Volume = catalog["volume"] != null ? catalog.Value<float>("volume") : DefaultVolume
            };
            return true;
        }

        public static AudioCatalogMetadata ReadOrDefault(AssetImporter importer)
        {
            return TryRead(importer, out var metadata) ? metadata : new AudioCatalogMetadata();
        }

        public static void Write(AssetImporter importer, AudioCatalogMetadata metadata)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            var catalog = new JObject
            {
                ["included"] = metadata.Included,
                ["group"] = metadata.Group ?? string.Empty,
                ["volume"] = metadata.Volume
            };

            if (AssetUserData.Write(importer, UserDataKey, LogTag, catalog))
                AudioGroupsRegistry.Instance.Invalidate();
        }
    }
}
