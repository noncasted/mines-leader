using System;
using System.Collections.Generic;
using GamePlay.Services;
using GamePlay.UI;
using Internal;
using Meta;
using Shared;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GamePlay.Players.Buffs
{
    public class PlayerBuffsList : IUpdatable
    {
        public PlayerBuffsList(
            RectTransform container,
            PlayerBuffView viewPrefab,
            PlayerBuffInfo info,
            ICardsRegistry cards,
            IGameInput input,
            ModifierDescriptionsConfig descriptions,
            bool showOnRight)
        {
            _container = container;
            _viewPrefab = viewPrefab;
            _info = info;
            _cards = cards;
            _input = input;
            _descriptions = descriptions;
            _showOnRight = showOnRight;

            HidePlaceholders();
        }

        private readonly RectTransform _container;
        private readonly PlayerBuffView _viewPrefab;
        private readonly PlayerBuffInfo _info;
        private readonly ICardsRegistry _cards;
        private readonly IGameInput _input;
        private readonly ModifierDescriptionsConfig _descriptions;
        private readonly bool _showOnRight;
        private readonly Dictionary<Guid, PlayerBuffView> _views = new();

        private Guid _hoveredId;

        public void Bind(IReadOnlyLifetime lifetime, IPlayerModifiers modifiers)
        {
            modifiers.Overviews.View(lifetime, (itemLifetime, overview) =>
            {
                if (_views.TryGetValue(overview.SourceId, out var existing))
                {
                    existing.Refresh(overview);
                    return;
                }

                var view = Object.Instantiate(_viewPrefab, _container);
                view.Setup(overview, _cards.GetModifierSprite(overview.Type));
                _views[overview.SourceId] = view;

                view.PointerHandler.IsHovered.View(itemLifetime, hovered =>
                {
                    if (hovered == true)
                        ShowInfo(overview);
                    else if (_hoveredId == overview.SourceId)
                        HideInfo();
                });

                itemLifetime.Listen(() => RemoveView(overview.SourceId, view));
            });
        }

        public void OnUpdate(float delta)
        {
            if (_hoveredId == Guid.Empty)
                return;

            _info.Follow(_input.Screen, _showOnRight);
        }

        private void ShowInfo(DurationalModifierOverview overview)
        {
            _hoveredId = overview.SourceId;
            _info.Show(GetDescription(overview));
            _info.Follow(_input.Screen, _showOnRight);
        }

        private void HideInfo()
        {
            _hoveredId = Guid.Empty;
            _info.Hide();
        }

        private string GetDescription(DurationalModifierOverview overview)
        {
            if (_descriptions == null)
                return overview.Key;

            return _descriptions.GetDescription(overview.Key);
        }

        private void HidePlaceholders()
        {
            for (var i = 0; i < _container.childCount; i++)
                _container.GetChild(i).gameObject.SetActive(false);
        }

        private void RemoveView(Guid sourceId, PlayerBuffView view)
        {
            _views.Remove(sourceId);

            if (_hoveredId == sourceId)
                HideInfo();

            if (view != null)
                Object.Destroy(view.gameObject);
        }
    }
}
