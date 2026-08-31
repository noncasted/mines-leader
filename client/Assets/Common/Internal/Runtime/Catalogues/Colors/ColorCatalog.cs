using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using Sirenix.OdinInspector;
#endif

namespace Internal {
    [Serializable]
    public sealed class ColorEntry {
        public string Name;
        public Color Value = Color.white;
    }

    [Serializable]
    public sealed class ColorGroup {
        public string Name;
        public List<ColorEntry> Entries = new();
    }

    [CreateAssetMenu(fileName = "ColorCatalog", menuName = "Tools/Color Catalog")]
    public sealed class ColorCatalog : ScriptableObject {
        [SerializeField] private List<ColorGroup> _groups = new();

        public IReadOnlyList<ColorGroup> Groups => _groups;

#if UNITY_EDITOR
        [Button("Refresh")]
        private void Refresh() {
            UnityEditor.EditorApplication.ExecuteMenuItem("Tools/GenerateColors");
        }
#endif
    }
}
