using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Global.UI;
using Internal;
using Meta;
using UnityEngine;

namespace Menu.Unlocks
{
    public interface IMenuUnlocks : IUIState
    {
    }

    public class MenuUnlocks : IMenuUnlocks, IScopeSetup, IUIStateAsyncEnterHandler
    {
        public MenuUnlocks(
            IAchievements achievements,
            IMenuUnlockSelection selection,
            MenuUnlocksBindings bindings)
        {
            _achievements = achievements;
            _selection = selection;

            _rowsRoot = bindings.Rows.Viewport.Content.RectTransform;
            _gameObject = bindings.GameObject;

            _gameObject.SetActive(false);
        }

        private readonly RectTransform _rowsRoot;
        private readonly GameObject _gameObject;

        private readonly List<EntryView> _entries = new();

        private readonly IAchievements _achievements;
        private readonly IMenuUnlockSelection _selection;

        private IReadOnlyLifetime _scopeLifetime;
        private IReadOnlyLifetime _screenLifetime;

        public IUIConstraints Constraints { get; } = UIConstraints.Game;

        /// <summary>
        /// Rows are built once at scene start and live together with it.
        /// </summary>
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _scopeLifetime = lifetime;
            _achievements.Rows.View(lifetime, (rowLifetime, row) => BuildRow(lifetime, rowLifetime, row));
        }

        /// <summary>
        /// On entry we only subscribe: statuses and clicks live exactly as long as the screen is open.
        /// </summary>
        public UniTask OnEntered(IUIStateHandle handle)
        {
            handle.AttachGameObject(_gameObject);

            _screenLifetime = handle.InnerLifetime;
            handle.InnerLifetime.Listen(() => _screenLifetime = null);

            foreach (var entry in _entries)
                Bind(entry);

            return UniTask.CompletedTask;
        }

        private void BuildRow(IReadOnlyLifetime screenLifetime, IReadOnlyLifetime rowLifetime, IAchievementRow row)
        {
            // The row lives while both the scene and the config entry exist.
            var lifetime = rowLifetime.Child();
            screenLifetime.Listen(lifetime.Terminate);

            var rowObject = Object.Instantiate(MenuPrefabs.MenuUnlocksRow, _rowsRoot);
            rowObject.name = $"Row_{row.Type}";
            var rowTransform = (RectTransform)rowObject.transform;
            rowTransform.SetParent(_rowsRoot, false);

            foreach (var tier in row.Tiers)
            {
                var view = Object.Instantiate(MenuPrefabs.MenuUnlocksEntry, rowTransform);
                view.Setup(row, tier);

                var entry = new EntryView(view, lifetime);
                _entries.Add(entry);
                Bind(entry);
            }

            lifetime.Listen(() => {
                _entries.RemoveAll(entry => entry.Lifetime == lifetime);

                if (rowObject != null)
                    Object.Destroy(rowObject);
            });
        }

        private void Bind(EntryView entry)
        {
            // A row may have arrived with the config after entering the screen, so the subscription goes from here too.
            if (_screenLifetime == null)
                return;

            entry.View.Bind(_screenLifetime.Intersect(entry.Lifetime), OnEntryClicked);
        }

        private void OnEntryClicked(MenuUnlockEntry entry)
        {
            // The selection is a child of the global base state: it is bound to the menu scope, not to this screen.
            _selection.Process(_scopeLifetime, entry.Tier).Forget();
        }

        private readonly struct EntryView
        {
            public EntryView(MenuUnlockEntry view, IReadOnlyLifetime lifetime)
            {
                View = view;
                Lifetime = lifetime;
            }

            public MenuUnlockEntry View { get; }
            public IReadOnlyLifetime Lifetime { get; }
        }
    }
}
