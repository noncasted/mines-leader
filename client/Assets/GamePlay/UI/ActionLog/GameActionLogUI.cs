using System.Collections.Generic;
using System.Linq;
using Global.UI.Toolkit;
using Internal;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace GamePlay.UI.ActionLog
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public class GameActionLogUI : MonoBehaviour, ISceneService, IScopeSetupCompletion
    {
        [SerializeField] private VisualTreeAsset _tileTemplate;

        private IGameActionLog _log;

        [Inject]
        private void Construct(IGameActionLog log)
        {
            _log = log;
        }

        private readonly List<VisualElement> _tiles = new();

        private VisualElement _tileContainer;
        private VisualElement _tooltip;
        private Label _tooltipName;
        private Label _tooltipDesc;

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IScopeSetupCompletion>();
        }

        public void OnSetupCompletion(IReadOnlyLifetime lifetime)
        {
            Debug.Log("[GameActionLogUI] OnSetup start");

            var document = GetComponent<UIDocument>();
            if (document == null)
            {
                Debug.LogError("[GameActionLogUI] UIDocument component missing");
                return;
            }

            var root = document.rootVisualElement;
            if (root == null)
            {
                Debug.LogError("[GameActionLogUI] rootVisualElement is null");
                return;
            }

            _tileContainer = root.Q<VisualElement>("tile-container");
            if (_tileContainer == null)
            {
                Debug.LogError("[GameActionLogUI] tile-container not found in UXML");
                return;
            }

            var defaultTile = _tileContainer.Children().FirstOrDefault();
            if (defaultTile != null)
            {
                defaultTile.style.display = DisplayStyle.None;
                Debug.Log("[GameActionLogUI] Default tile hidden");
            }
            else
            {
                Debug.LogWarning("[GameActionLogUI] No default tile found in tile-container");
            }

            _tooltip = root.Q<VisualElement>("tooltip");
            _tooltipName = root.Q<Label>("tooltip-name");
            _tooltipDesc = root.Q<Label>("tooltip-desc");

            if (_tooltip == null)
                Debug.LogError("[GameActionLogUI] tooltip not found in UXML");
            if (_tooltipName == null)
                Debug.LogError("[GameActionLogUI] tooltip-name not found in UXML");
            if (_tooltipDesc == null)
                Debug.LogError("[GameActionLogUI] tooltip-desc not found in UXML");

            if (_tooltip != null)
            {
                _tooltip.style.display = DisplayStyle.None;
                Debug.Log("[GameActionLogUI] Tooltip hidden");
            }
            else
            {
                Debug.LogWarning("[GameActionLogUI] Tooltip not found, cannot hide");
            }

            if (_tileTemplate == null)
            {
                Debug.LogError("[GameActionLogUI] _tileTemplate is null — assign GameActionLogTile.uxml in Inspector!");
                return;
            }

            if (_log == null)
            {
                Debug.LogError("[GameActionLogUI] _log is null — IGameActionLog was not injected!");
                return;
            }

            Debug.Log($"[GameActionLogUI] _log entries: {_log.Entries.Count}");

            _log.Entries.Advise(lifetime, (entryLifetime, entry) =>
            {
                Debug.Log($"[GameActionLogUI] New entry: {entry.PlayerName}: {entry.Message}");
                var tile = CreateTile(entry);
                if (tile != null)
                    _tiles.Add(tile);
                
                TrimExcessTiles();

                entryLifetime.Listen(() =>
                {
                    _tiles.Remove(tile);
                    tile?.RemoveFromHierarchy();
                    RefreshOpacity();
                });

                RefreshOpacity();
            });

            foreach (var entry in _log.Entries)
            {
                var tile = CreateTile(entry);
                if (tile != null)
                    _tiles.Add(tile);
            }
            
            TrimExcessTiles();
            RefreshOpacity();

            Debug.Log($"[GameActionLogUI] OnSetup complete. Tiles: {_tiles.Count}");
        }

        private VisualElement CreateTile(GameActionLogEntry entry)
        {
            if (_tileTemplate == null)
                return null;

            var templateContainer = _tileTemplate.Instantiate();
            var tileRoot = templateContainer.Q<VisualElement>("tile-root")
                            ?? templateContainer.contentContainer
                            ?? templateContainer;

            if (tileRoot == null)
                return null;

            var messageLabel = tileRoot.Q<Label>("tile-message");
            if (messageLabel != null)
                messageLabel.text = $"{entry.PlayerName}: {entry.Message}";

            tileRoot.AddToClassList(GetClassForType(entry.Type));

            tileRoot.RegisterCallback<PointerEnterEvent>(_ => OnTileHoverEnter(entry, tileRoot));
            tileRoot.RegisterCallback<PointerLeaveEvent>(_ => OnTileHoverExit());

            _tileContainer.Add(tileRoot);
            Debug.Log($"[GameActionLogUI] Tile created: {entry.Message}");

            return tileRoot;
        }

        private void OnTileHoverEnter(GameActionLogEntry entry, VisualElement tile)
        {
            if (_tooltip == null)
                return;

            if (string.IsNullOrEmpty(entry.CardName))
                return;

            _tooltipName.text = entry.CardName;
            _tooltipDesc.text = entry.CardDescription ?? "";
            _tooltipDesc.style.display = string.IsNullOrEmpty(entry.CardDescription)
                ? DisplayStyle.None
                : DisplayStyle.Flex;

            var tileWorldBounds = tile.worldBound;
            var rootLocal = _tooltip.parent != null
                ? _tooltip.parent.WorldToLocal(tileWorldBounds.position)
                : tileWorldBounds.position;

            _tooltip.style.left = rootLocal.x + tileWorldBounds.width + 4f;
            _tooltip.style.top = rootLocal.y;
            _tooltip.style.display = DisplayStyle.Flex;
        }

        private void OnTileHoverExit()
        {
            if (_tooltip != null)
                _tooltip.style.display = DisplayStyle.None;
        }

        private void TrimExcessTiles()
        {
            const int maxTiles = 5;
            while (_tiles.Count > maxTiles)
            {
                var oldest = _tiles[0];
                _tiles.RemoveAt(0);
                oldest?.RemoveFromHierarchy();
            }
        }

        private void RefreshOpacity()
        {
            var count = _tiles.Count;
            for (var i = 0; i < count; i++)
            {
                var distanceFromNewest = count - 1 - i;
                var alpha = 1f - distanceFromNewest * 0.2f;
                _tiles[i].style.opacity = Mathf.Clamp01(alpha);
            }
        }

        private static string GetClassForType(GameActionLogEntryType type)
        {
            return type switch
            {
                GameActionLogEntryType.CardPlayedSelf => "tile-self",
                GameActionLogEntryType.CardPlayedOpponent => "tile-opponent",
                GameActionLogEntryType.ManaChanged => "tile-mana",
                GameActionLogEntryType.HealthChanged => "tile-health",
                GameActionLogEntryType.MaxMovesChanged => "tile-moves",
                _ => "tile-self",
            };
        }
    }
}
