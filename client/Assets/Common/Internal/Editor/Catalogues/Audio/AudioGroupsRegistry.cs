using System;
using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;

namespace Internal
{
    [NoAutoStaticsCleanup]
    public sealed class AudioGroupsRegistry : CatalogGroupsRegistry
    {
        private AudioGroupsRegistry()
            : base("AudioGroupsRegistry", "Assets/Common/Internal/Editor/Catalogues/Audio/AudioGroups.json")
        {
        }

        public static AudioGroupsRegistry Instance { get; } = new();

        protected override IEnumerable<string> DiscoverGroups()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var source in AudioCatalogGenerator.EnumerateMarked(LogTag))
            {
                var metadata = source.Metadata;

                if (metadata.Included && string.IsNullOrEmpty(metadata.Group) == false && seen.Add(metadata.Group))
                    yield return metadata.Group;
            }
        }
    }
}
