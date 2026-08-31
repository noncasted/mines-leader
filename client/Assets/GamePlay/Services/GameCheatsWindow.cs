using System.Collections.Generic;
using GamePlay.Loop;
using Internal;
using Meta;
using Shared;
using UnityEngine;

namespace GamePlay.Cheats {
    public struct CheatCardInfo {
        public int TypeId;
        public string Name;
        public Sprite Icon;
    }

    public static class GameCheatsBridge {
        public static bool IsActive { get; set; }

        private static INetworkConnection _connection;
        private static ICardsRegistry _cards;
        private static IGameContext _gameContext;

        public static void Set(INetworkConnection connection, ICardsRegistry cards, IGameContext gameContext) {
            _connection = connection;
            _cards = cards;
            _gameContext = gameContext;
            IsActive = true;
        }

        public static void Clear() {
            _connection = null;
            _cards = null;
            _gameContext = null;
            IsActive = false;
        }

        public static List<CheatCardInfo> GetCards() {
            var result = new List<CheatCardInfo>();

            if (_cards == null)
                return result;

            foreach (var (type, definition) in _cards.Entries) {
                result.Add(new CheatCardInfo {
                    TypeId = (int)type,
                    Name = definition.Name,
                    Icon = definition.Image
                });
            }

            return result;
        }

        // Health
        public static void ChangeHealth(int delta) => Send(new GameCheatContexts.ChangeHealth { Value = delta });
        public static void ChangeMaxHealth(int delta) => Send(new GameCheatContexts.ChangeMaxHealth { Value = delta });
        public static void SetMaxHealth(int value) => Send(new GameCheatContexts.SetMaxHealth { Value = value });
        public static void RestoreHealth() => Send(new GameCheatContexts.RestoreHealth());

        // Mana
        public static void ChangeMana(int delta) => Send(new GameCheatContexts.ChangeMana { Value = delta });
        public static void ChangeMaxMana(int delta) => Send(new GameCheatContexts.ChangeMaxMana { Value = delta });
        public static void SetMaxMana(int value) => Send(new GameCheatContexts.SetMaxMana { Value = value });
        public static void RestoreMana() => Send(new GameCheatContexts.RestoreMana());

        // Turns
        public static void ChangeMoves(int delta) => Send(new GameCheatContexts.ChangeMoves { Value = delta });
        public static void ChangeMaxMoves(int delta) => Send(new GameCheatContexts.ChangeMaxMoves { Value = delta });
        public static void SetMaxMoves(int value) => Send(new GameCheatContexts.SetMaxMoves { Value = value });
        public static void RestoreMoves() => Send(new GameCheatContexts.RestoreMoves());

        // Cards
        public static void AddCard(int typeId) => Send(new GameCheatContexts.CardAdd { Type = (CardType)typeId });

        // Match
        public static void EndMatch(bool win) {
            if (_gameContext == null) return;
            var winner = win ? _gameContext.Self.Id : _gameContext.Other.Id;
            Send(new GameCheatContexts.EndMatch { Winner = winner });
        }

        private static void Send(INetworkContext context) {
            if (_connection == null) {
                Debug.LogWarning("[GameCheats] Not connected - start a game first");
                return;
            }

            _connection.Request(context);
        }
    }

    public class GameCheatsService : IScopeSetup {
        public GameCheatsService(
            INetworkConnection connection,
            ICardsRegistry cards,
            IGameContext gameContext) {
            _connection = connection;
            _cards = cards;
            _gameContext = gameContext;
        }

        private readonly INetworkConnection _connection;
        private readonly ICardsRegistry _cards;
        private readonly IGameContext _gameContext;

        public void OnSetup(IReadOnlyLifetime lifetime) {
            GameCheatsBridge.Set(_connection, _cards, _gameContext);
            lifetime.Listen(GameCheatsBridge.Clear);
        }
    }
}
