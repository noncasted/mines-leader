using System.Collections.Generic;
using Exoa.Responsive;
using Internal;
using TMPro;
using Tools;
using Tools.Runtime.PrefabBuilder;
using UnityEngine;
using VContainer;

namespace GamePlay.UI.ActionLog
{
    public class GameActionLogUI : MonoBehaviour, ISceneService, IScopeSetup
    {
        [SerializeField] private RectTransform _container;
        [SerializeField] private CanvasGroup _tooltipGroup;
        [SerializeField] private TMP_Text _tooltipCardName;
        [SerializeField] private TMP_Text _tooltipCardDescription;
        [SerializeField] private ResponsiveContainer _responsiveContainer;

        [Inject] private IGameActionLog _log;

        private readonly List<GameActionLogTileUI> _activeTiles = new();

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            HideTooltip();

            _log.Entries.View(lifetime, (entryLifetime, entry) => {
                var tile = CreateTile(entry);
                _activeTiles.Add(tile);

                entryLifetime.Listen(() => {
                    _activeTiles.Remove(tile);
                    Destroy(tile.gameObject);
                    RefreshOpacity();
                    RefreshLayout();
                });

                RefreshOpacity();
                RefreshLayout();
            });
        }

        private GameActionLogTileUI CreateTile(GameActionLogEntry entry)
        {
            var prefab = Prefabs.ActionLogTile.As<GameActionLogTileUI>();
            var tile = Instantiate(prefab, _container);
            tile.Setup(entry, OnTileHoverEnter, OnTileHoverExit);
            return tile;
        }

        private void RefreshOpacity()
        {
            for (var i = 0; i < _activeTiles.Count; i++)
            {
                var isNewest = i == _activeTiles.Count - 1;
                _activeTiles[i].SetOpacity(isNewest ? 1f : 0.5f);
            }
        }

        private void RefreshLayout()
        {
            if (_responsiveContainer != null)
                _responsiveContainer.Resize(true);
        }

        private void OnTileHoverEnter(GameActionLogTileUI tile)
        {
            if (tile.Entry.CardName == null)
                return;

            _tooltipCardName.text = tile.Entry.CardName;
            _tooltipCardDescription.text = tile.Entry.CardDescription;
            _tooltipGroup.alpha = 1f;
            _tooltipGroup.gameObject.SetActive(true);

            var tooltipRt = (RectTransform)_tooltipGroup.transform;
            var tileWidth = tile.RectTransform.rect.width;

            tooltipRt.anchoredPosition = new Vector2(tileWidth + 8f, 0f);
        }

        private void OnTileHoverExit(GameActionLogTileUI tile)
        {
            HideTooltip();
        }

        private void HideTooltip()
        {
            _tooltipGroup.alpha = 0f;
            _tooltipGroup.gameObject.SetActive(false);
        }
    }
}
