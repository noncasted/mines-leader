using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace Global.Publisher.Itch
{
    public class ItchSaves : ISaves, IScopeBaseSetup
    {
        // JsonUtility пишет [field: SerializeField] как "<Name>k__BackingField", поэтому сейвы
        // старого ключа (писал Newtonsoft) не читаются: они удаляются, настройки сбрасываются один раз.
        private const string Key = "save_v2";
        private const string LegacyKey = "save";

        private readonly Dictionary<string, string> _entries = new();

        public void OnBaseSetup(IReadOnlyLifetime lifetime)
        {
            if (PlayerPrefs.HasKey(LegacyKey) == true)
                PlayerPrefs.DeleteKey(LegacyKey);

            if (PlayerPrefs.HasKey(Key) == false)
                return;

            var raw = PlayerPrefs.GetString(Key);
            var container = TryParse<SaveEntries>(raw);

            if (container?.Entries == null)
                return;

            foreach (var entry in container.Entries)
            {
                if (string.IsNullOrEmpty(entry.Key) == true)
                    continue;

                _entries[entry.Key] = entry.Value;
            }
        }

        public T Get<T>() where T : class, new()
        {
            var key = typeof(T).FullName!;

            if (_entries.TryGetValue(key, out var rawEntry) == false)
                return new T();

            return TryParse<T>(rawEntry) ?? new T();
        }

        public UniTask Save<T>(T data)
        {
            var key = typeof(T).FullName!;
            _entries[key] = JsonUtility.ToJson(data);

            var container = new SaveEntries();

            foreach (var (entryKey, value) in _entries)
                container.Entries.Add(new SaveEntry { Key = entryKey, Value = value });

            PlayerPrefs.SetString(Key, JsonUtility.ToJson(container));
            return UniTask.CompletedTask;
        }

        private static T TryParse<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json) == true)
                return null;

            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[ItchSaves] Failed to parse {typeof(T).Name}: {exception.Message}");
                return null;
            }
        }

        [Serializable]
        private class SaveEntries
        {
            public List<SaveEntry> Entries = new();
        }

        [Serializable]
        private class SaveEntry
        {
            public string Key;
            public string Value;
        }
    }
}
