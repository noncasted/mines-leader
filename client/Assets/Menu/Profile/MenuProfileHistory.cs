using System;
using System.Collections.Generic;
using Internal;
using Meta;
using UnityEngine;

namespace Menu.Profile
{
    /// <summary>
    /// Список матчей: строки спавнятся из префаба в один контейнер и живут
    /// до следующей сборки списка.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuProfileHistory : MonoBehaviour
    {
        [SerializeField] private RectTransform _root;

        private readonly List<MenuProfileHistoryEntry> _entries = new();

        public void Clear()
        {
            foreach (var entry in _entries)
            {
                if (entry != null)
                    Destroy(entry.gameObject);
            }

            _entries.Clear();
        }

        public void Build(
            IReadOnlyLifetime lifetime,
            IReadOnlyList<ProfileMatchEntry> matches,
            Func<ProfileMatchEntry, string> modeName,
            Action<ProfileMatchEntry> selected)
        {
            Clear();

            foreach (var match in matches)
            {
                var view = Instantiate(MenuPrefabs.MenuProfileHistoryEntry, _root);
                view.name = $"Match_{match.Id:N}";
                view.Setup(match, modeName.Invoke(match));
                view.Bind(lifetime, entry => selected.Invoke(entry.Match));
                _entries.Add(view);
            }
        }

        public void SetSelected(Guid matchId)
        {
            foreach (var entry in _entries)
                entry.SetSelected(entry.Match.Id == matchId);
        }
    }
}
