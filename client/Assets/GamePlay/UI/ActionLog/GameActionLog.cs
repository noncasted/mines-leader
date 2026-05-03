using System;
using GamePlay.Loop;
using Internal;
using Meta;
using Shared;
using UnityEngine;

namespace GamePlay.UI.ActionLog
{
    public interface IGameActionLog
    {
        ViewableList<GameActionLogEntry> Entries { get; }
        void LogCardAction(Guid playerId, CardType cardType);
        void LogResourceChange(Guid playerId, GameActionLogEntryType type, string resourceName, int oldValue, int newValue);
    }

    public class GameActionLog : IGameActionLog
    {
        private const int MaxEntries = 5;

        public GameActionLog(IGameContext gameContext, ICardsRegistry cardsRegistry)
        {
            _gameContext = gameContext;
            _cardsRegistry = cardsRegistry;
            Debug.Log("[GameActionLog] Service created");
        }

        private readonly IGameContext _gameContext;
        private readonly ICardsRegistry _cardsRegistry;

        public ViewableList<GameActionLogEntry> Entries { get; } = new();

        public void LogCardAction(Guid playerId, CardType cardType)
        {
            Debug.Log($"[GameActionLog] LogCardAction called: player={playerId}, card={cardType}");
            if (!_cardsRegistry.Entries.TryGetValue(cardType, out var definition))
            {
                Debug.LogWarning($"[GameActionLog] Card definition not found for {cardType}");
                return;
            }

            var isLocal = playerId == _gameContext.Self.Id;

            var entry = new GameActionLogEntry
            {
                Type = isLocal ? GameActionLogEntryType.CardPlayedSelf : GameActionLogEntryType.CardPlayedOpponent,
                PlayerName = isLocal ? "You" : "Opponent",
                Message = definition.Name,
                CardType = cardType,
                CardName = definition.Name,
                CardDescription = definition.Description,
            };

            AddEntry(entry);
        }

        public void LogResourceChange(Guid playerId, GameActionLogEntryType type, string resourceName, int oldValue, int newValue)
        {
            Debug.Log($"[GameActionLog] LogResourceChange called: player={playerId}, type={type}, {resourceName}: {oldValue} > {newValue}");
            if (oldValue == newValue)
                return;

            if (!_gameContext.IsGameStarted)
                return;

            var isLocal = playerId == _gameContext.Self.Id;
            var delta = newValue - oldValue;
            var sign = delta > 0 ? "+" : "";

            var entry = new GameActionLogEntry
            {
                Type = type,
                PlayerName = isLocal ? "You" : "Opponent",
                Message = $"{resourceName}: {sign}{delta} ({newValue})",
            };

            AddEntry(entry);
        }

        private void AddEntry(GameActionLogEntry entry)
        {
            if (Entries.Count >= MaxEntries)
                Entries.RemoveAt(0);

            Debug.Log($"[GameActionLog] Adding entry: {entry.PlayerName}: {entry.Message}. Count before add: {Entries.Count}");
            Entries.Add(entry);
            Debug.Log($"[GameActionLog] Entry added. Count after add: {Entries.Count}");
        }
    }
}
