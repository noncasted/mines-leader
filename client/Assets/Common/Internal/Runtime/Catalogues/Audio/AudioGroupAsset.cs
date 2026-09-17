using System;
using UnityEngine;

namespace Internal
{
    [Serializable]
    public sealed class AudioAssetEntry
    {
        public string Name;
        public AudioClip Clip;
        [Range(0f, 1f)] public float Volume = 1f;
    }

    public sealed class AudioGroupAsset : ScriptableObject
    {
        [SerializeField] private AudioAssetEntry[] _entries = Array.Empty<AudioAssetEntry>();

        public Sound Get(string name)
        {
            var entry = FindEntry(name);

            if (entry == null || entry.Clip == null)
                throw new InvalidOperationException($"Audio clip '{name}' is missing in {this.name}");

            return new Sound(entry.Clip, entry.Volume);
        }

        private AudioAssetEntry FindEntry(string name)
        {
            if (_entries == null)
                return null;

            for (var i = 0; i < _entries.Length; i++)
            {
                var entry = _entries[i];

                if (entry == null)
                    continue;

                if (string.Equals(entry.Name, name, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }
    }
}
