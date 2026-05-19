using System.Collections.Generic;
using UnityEngine;

namespace GamePlay.UI
{
    [CreateAssetMenu(fileName = "ModifierDescriptionsConfig", menuName = "Game/Modifier Descriptions Config")]
    public class ModifierDescriptionsConfig : ScriptableObject
    {
        [SerializeField] private List<Entry> _entries = new();

        private Dictionary<string, string> _descriptionLookup;
        private Dictionary<string, Sprite> _iconLookup;

        public string GetDescription(string key)
        {
            BuildLookups();
            return _descriptionLookup.TryGetValue(key, out var description) ? description : key;
        }

        public Sprite GetIcon(string key)
        {
            BuildLookups();
            return _iconLookup.TryGetValue(key, out var icon) ? icon : null;
        }

        private void BuildLookups()
        {
            if (_descriptionLookup != null && _iconLookup != null)
                return;

            _descriptionLookup = new Dictionary<string, string>();
            _iconLookup = new Dictionary<string, Sprite>();

            foreach (var entry in _entries)
            {
                _descriptionLookup[entry.Key] = entry.Description;
                _iconLookup[entry.Key] = entry.Icon;
            }
        }

        [System.Serializable]
        public class Entry
        {
            public string Key;
            public string Description;
            public Sprite Icon;
        }
    }
}
