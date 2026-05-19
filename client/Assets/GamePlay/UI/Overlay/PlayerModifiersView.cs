using System;
using System.Collections.Generic;
using System.Linq;
using GamePlay.Loop;
using GamePlay.Players;
using Internal;
using Shared;
using UnityEngine;
using VContainer;
using Lifetime = Internal.Lifetime;

namespace GamePlay.UI
{
    [DisallowMultipleComponent]
    public class PlayerModifiersView : MonoBehaviour, ISceneService, IScopeSetup
    {
        [SerializeField] private Transform _container;
        [SerializeField] private ModifierDescriptionsConfig _descriptionsConfig;
        [SerializeField] private PlayerModifierTooltipView _tooltipPrefab;
        [SerializeField] private PlayerModifierEntryView _entryPrefab;
        [SerializeField] private Vector2 _tooltipOffset;

        private IGameContext _gameContext;
        private readonly Dictionary<Guid, EntryData> _entries = new();
        private PlayerModifierTooltipView _tooltip;
        private bool _isSubscribed;

        [Inject]
        private void Construct(IGameContext gameContext)
        {
            _gameContext = gameContext;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _gameContext.Updated.Advise(lifetime, () => OnContextUpdated(lifetime));
        }

        private void OnContextUpdated(IReadOnlyLifetime lifetime)
        {
            if (_isSubscribed)
                return;

            _isSubscribed = true;

            var self = _gameContext.Self;
            if (self == null)
                return;

            foreach (var overview in self.Modifiers.Overviews.ToList())
                CreateOrUpdateEntry(overview);

            self.Modifiers.Overviews.Advise(lifetime, (entryLifetime, overview) =>
            {
                CreateOrUpdateEntry(overview);
                entryLifetime.Listen(() => RemoveEntry(overview.SourceId));
            });
        }

        private void CreateOrUpdateEntry(DurationalModifierOverview overview)
        {
            if (_entries.TryGetValue(overview.SourceId, out var data))
            {
                data.Entry.UpdateTurns(overview.TurnsToEnd);
                return;
            }

            var entryLifetime = new Lifetime();
            var entry = Instantiate(_entryPrefab, _container);
            var icon = GetIconForKey(overview.Key);
            entry.Setup(overview, icon);

            entry.PointerHandler.IsHovered.View(entryLifetime, hovered =>
            {
                if (hovered)
                    ShowTooltip(entry, overview.Key);
                else
                    HideTooltip();
            });

            _entries[overview.SourceId] = new EntryData { Entry = entry, Lifetime = entryLifetime };
        }

        private void RemoveEntry(Guid sourceId)
        {
            if (_entries.TryGetValue(sourceId, out var data) == false)
                return;

            data.Lifetime.Terminate();
            _entries.Remove(sourceId);
            Destroy(data.Entry.gameObject);
        }

        private void ShowTooltip(PlayerModifierEntryView entry, string key)
        {
            if (_tooltip == null)
                _tooltip = Instantiate(_tooltipPrefab, _container.parent);

            var description = _descriptionsConfig.GetDescription(key);
            var position = entry.transform.position;
            position.x += _tooltipOffset.x;
            position.y += _tooltipOffset.y;
            _tooltip.Show(description, position);
        }

        private void HideTooltip()
        {
            if (_tooltip != null)
                _tooltip.Hide();
        }

        private Sprite GetIconForKey(string key)
        {
            return _descriptionsConfig?.GetIcon(key);
        }

        private class EntryData
        {
            public PlayerModifierEntryView Entry;
            public ILifetime Lifetime;
        }
    }
}
