#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using Meta;
using Shared;

namespace GamePlay.Cheats
{
    public class GameCheats : IGameCheats, IScopeSetup
    {
        public GameCheats(INetworkConnection connection, ICardsRegistry cards, IGameContext gameContext)
        {
            _connection = connection;
            _cards = cards;
            _gameContext = gameContext;
        }

        private readonly INetworkConnection _connection;
        private readonly ICardsRegistry _cards;
        private readonly IGameContext _gameContext;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            GameCheatsAccess.Attach(lifetime, this);
        }

        public IReadOnlyList<CheatCardInfo> GetCards()
        {
            var result = new List<CheatCardInfo>();

            foreach (var (type, definition) in _cards.Entries)
                result.Add(new CheatCardInfo((int)type, definition.Name, definition.Image));

            return result;
        }

        public void ChangeHealth(int delta) => Send(new GameCheatContexts.ChangeHealth { Value = delta });
        public void ChangeMaxHealth(int delta) => Send(new GameCheatContexts.ChangeMaxHealth { Value = delta });
        public void SetMaxHealth(int value) => Send(new GameCheatContexts.SetMaxHealth { Value = value });
        public void RestoreHealth() => Send(new GameCheatContexts.RestoreHealth());

        public void ChangeMana(int delta) => Send(new GameCheatContexts.ChangeMana { Value = delta });
        public void ChangeMaxMana(int delta) => Send(new GameCheatContexts.ChangeMaxMana { Value = delta });
        public void SetMaxMana(int value) => Send(new GameCheatContexts.SetMaxMana { Value = value });
        public void RestoreMana() => Send(new GameCheatContexts.RestoreMana());

        public void ChangeMoves(int delta) => Send(new GameCheatContexts.ChangeMoves { Value = delta });
        public void ChangeMaxMoves(int delta) => Send(new GameCheatContexts.ChangeMaxMoves { Value = delta });
        public void SetMaxMoves(int value) => Send(new GameCheatContexts.SetMaxMoves { Value = value });
        public void RestoreMoves() => Send(new GameCheatContexts.RestoreMoves());

        public void AddCard(int typeId) => Send(new GameCheatContexts.CardAdd { Type = (CardType)typeId });

        public void EndMatch(bool win)
        {
            var winner = win ? _gameContext.Self.Id : _gameContext.Other.Id;
            Send(new GameCheatContexts.EndMatch { Winner = winner });
        }

        // A refusal of the server (cheats off) comes back as an exception in the log of the request.
        private void Send(INetworkContext context)
        {
            _connection.Request(context).Forget();
        }
    }
}
#endif
