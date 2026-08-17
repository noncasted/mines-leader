using System;
using GamePlay.Boards;
using GamePlay.Loop;
using GamePlay.Players;
using Internal;
using Meta;
using Shared;

namespace GamePlay.Cards
{
    public interface ICardContext
    {
        IBoard TargetBoard { get; }
        IViewableProperty<bool> IsAvailable { get; }
        CardType Type { get; }
        ICardConfig Config { get; }
    }

    public class CardContext : ICardContext, IScopeSetup
    {
        public CardContext(
            IGameContext gameContext,
            IPlayerMana mana,
            IGameRound gameRound,
            IPlayerMoves moves,
            ICardDefinition definition,
            ICardConfig config)
        {
            _gameContext = gameContext;
            _mana = mana;
            _gameRound = gameRound;
            _moves = moves;
            _definition = definition;
            Config = config;

            TargetBoard = config.Target switch
            {
                CardTarget.OwnBoard => gameContext.Self.Board,
                CardTarget.OpponentBoard => gameContext.Other.Board,
                CardTarget.Self => null,
                CardTarget.Opponent => null,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private readonly IGameContext _gameContext;
        private readonly IPlayerMana _mana;
        private readonly IGameRound _gameRound;
        private readonly IPlayerMoves _moves;
        private readonly ICardDefinition _definition;

        private readonly ViewableProperty<bool> _isAvailable = new();

        public IBoard TargetBoard { get; }
        public IViewableProperty<bool> IsAvailable => _isAvailable;
        public CardType Type => _definition.Type;
        public ICardConfig Config { get; }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _mana.Current.View(lifetime, Recalculate);
            _gameRound.Player.Advise(lifetime, Recalculate);
            _moves.IsTurn.Advise(lifetime, Recalculate);
            _moves.Current.Advise(lifetime, Recalculate);
        }

        private void Recalculate()
        {
            if (_gameRound.IsTurnAllowed == false)
            {
                _isAvailable.Set(false);
                return;
            }

            if (Config.ManaCost > _mana.Current.Value)
            {
                _isAvailable.Set(false);
                return;
            }

            if (_moves.CanSpend(_gameContext, _gameContext.CardMovesCost) == false)
            {
                _isAvailable.Set(false);
                return;
            }

            _isAvailable.Set(true);
        }
    }
}