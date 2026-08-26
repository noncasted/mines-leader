using System;
using System.Collections.Generic;
using Shared;
using Tools;
using UnityEngine;

namespace Meta
{
    public interface IGameModesRegistry
    {
        IReadOnlyDictionary<GameMatchType, IGameModeDefinition> Entries { get; }
    }

    public class GameModesRegistry : IGameModesRegistry
    {
        public GameModesRegistry()
        {
            Load();
        }

        private readonly Dictionary<GameMatchType, IGameModeDefinition> _modes = new();

        public IReadOnlyDictionary<GameMatchType, IGameModeDefinition> Entries => _modes;

        private void Load()
        {
            var textAsset = Resources.Load<TextAsset>("game-modes-info");
            var payload = JsonUtility.FromJson<GameModesInfoPayload>(textAsset.text);

            foreach (var entry in payload.modes)
            {
                var type = (GameMatchType)Enum.Parse(typeof(GameMatchType), entry.type);
                var definition = new GameModeDefinition(type, entry.name, entry.description, TypeToSprite(type));
                _modes[type] = definition;
            }
        }

        private Sprite TypeToSprite(GameMatchType type)
        {
            return type switch
            {
                GameMatchType.TimeLimited => Sprites.MenuPlay.ModeTimeLimited,
                GameMatchType.LastManStanding => Sprites.MenuPlay.ModeLastManStanding,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}
