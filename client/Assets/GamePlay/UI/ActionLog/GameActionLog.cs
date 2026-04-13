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
    }

    public class GameActionLog : IGameActionLog
    {
        private const int MaxEntries = 5;

        public GameActionLog(IGameContext gameContext, ICardsRegistry cardsRegistry)
        {
            _gameContext = gameContext;
            _cardsRegistry = cardsRegistry;
        }

        private readonly IGameContext _gameContext;
        private readonly ICardsRegistry _cardsRegistry;

        public ViewableList<GameActionLogEntry> Entries { get; } = new();

        public void LogCardAction(Guid playerId, CardType cardType)
        {
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
                CardType = cardType,
                CardName = definition.Name,
                CardDescription = definition.Description,
            };

            if (Entries.Count >= MaxEntries)
                Entries.RemoveAt(0);

            Entries.Add(entry);
        }
    }
}