using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Global.UI;
using Internal;
using Meta;
using UnityEngine;
using VContainer;

namespace Menu.Unlocks
{
    public interface IMenuUnlocks : IUIState
    {
    }

    [DisallowMultipleComponent]
    public class MenuUnlocks : MonoBehaviour,
                               IMenuUnlocks,
                               ISceneService,
                               IScopeSetup,
                               IUIStateAsyncEnterHandler
    {
        [SerializeField] private RectTransform _rowsRoot;

        private readonly List<EntryView> _entries = new();

        private IAchievements _achievements;
        private IMenuUnlockSelection _selection;

        private IReadOnlyLifetime _screenLifetime;

        public IUIConstraints Constraints { get; } = UIConstraints.Game;

        [Inject]
        internal void Construct(IAchievements achievements, IMenuUnlockSelection selection)
        {
            _achievements = achievements;
            _selection = selection;
        }

        public void Create(IScopeBuilder builder)
        {
            gameObject.SetActive(false);

            builder.RegisterComponent(this)
                   .As<IMenuUnlocks>()
                   .As<IScopeSetup>();
        }

        /// <summary>
        /// Ряды собираются один раз на старте сцены и живут вместе с ней.
        /// </summary>
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _achievements.Rows.View(lifetime, (rowLifetime, row) => BuildRow(lifetime, rowLifetime, row));
        }

        /// <summary>
        /// На входе только подписываемся: статусы и клики живут ровно столько, сколько открыт экран.
        /// </summary>
        public UniTask OnEntered(IUIStateHandle handle)
        {
            handle.AttachGameObject(gameObject);

            _screenLifetime = handle.InnerLifetime;
            handle.InnerLifetime.Listen(() => _screenLifetime = null);

            foreach (var entry in _entries)
                Bind(entry);

            return UniTask.CompletedTask;
        }

        private void BuildRow(IReadOnlyLifetime screenLifetime, IReadOnlyLifetime rowLifetime, IAchievementRow row)
        {
            // Ряд живёт, пока существует и сама сцена, и запись в конфиге.
            var lifetime = rowLifetime.Child();
            screenLifetime.Listen(lifetime.Terminate);

            var rowObject = Instantiate(MenuPrefabs.MenuUnlocksRow, _rowsRoot);
            rowObject.name = $"Row_{row.Type}";
            var rowTransform = (RectTransform)rowObject.transform;
            rowTransform.SetParent(_rowsRoot, false);

            foreach (var tier in row.Tiers)
            {
                var view = Instantiate(MenuPrefabs.MenuUnlocksEntry, rowTransform);
                view.Setup(row, tier);

                var entry = new EntryView(view, lifetime);
                _entries.Add(entry);
                Bind(entry);
            }

            lifetime.Listen(() => {
                _entries.RemoveAll(entry => entry.Lifetime == lifetime);

                if (rowObject != null)
                    Destroy(rowObject);
            });
        }

        private void Bind(EntryView entry)
        {
            // Ряд мог приехать конфигом уже после входа на экран, поэтому подписка идёт и отсюда.
            if (_screenLifetime == null)
                return;

            entry.View.Bind(_screenLifetime.Intersect(entry.Lifetime), OnEntryClicked);
        }

        private void OnEntryClicked(MenuUnlockEntry entry)
        {
            _selection.Process(entry.Tier).Forget();
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
