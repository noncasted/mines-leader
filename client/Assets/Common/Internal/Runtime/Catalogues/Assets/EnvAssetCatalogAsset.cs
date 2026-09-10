using System;
using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    // Единственное хранилище каталога. Лежит в Resources, поэтому все перечисленные
    // ассеты гарантированно попадают в билд без Addressables.
    public sealed class EnvAssetCatalogAsset : ScriptableObject
    {
        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        public IReadOnlyList<Entry> Entries => _entries;

        [Serializable]
        public sealed class Entry
        {
            public string Group;
            public string Name;
            public EnvAsset Asset;
        }
    }
}