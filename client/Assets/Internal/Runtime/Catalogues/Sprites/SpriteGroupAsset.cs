using System;
using UnityEngine;

namespace Internal {
    public enum SpriteKind {
        Sheet,
        Animation
    }

    [Serializable]
    public sealed class SpriteEntry {
        public string Name;
        public SpriteKind Kind;
        public Sprite[] Sprites;
        public float Time;
        public Color Color;
    }

    public sealed class SpriteGroupAsset : ScriptableObject {
        [SerializeField] private SpriteEntry[] _entries;

        public Sprite GetSheet(string name) {
            var entry = FindEntry(name, SpriteKind.Sheet);
            if (entry == null || entry.Sprites == null || entry.Sprites.Length == 0)
                throw new InvalidOperationException($"Sheet '{name}' is missing in {this.name}");

            return entry.Sprites[0];
        }

        public ISpriteAnimationData GetAnimation(string name) {
            var entry = FindEntry(name, SpriteKind.Animation);
            if (entry == null || entry.Sprites == null || entry.Sprites.Length == 0)
                throw new InvalidOperationException($"Animation '{name}' is missing in {this.name}");

            return new SpriteAnimationData(entry.Sprites, entry.Time, entry.Color);
        }

        private SpriteEntry FindEntry(string name, SpriteKind kind) {
            if (_entries == null)
                return null;

            for (var i = 0; i < _entries.Length; i++) {
                var entry = _entries[i];
                if (entry == null)
                    continue;

                if (entry.Kind != kind)
                    continue;

                if (string.Equals(entry.Name, name, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }
    }
}
