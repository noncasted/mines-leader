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
    }

    public class CardContext : ICardContext, IScopeSetup
    {
        public CardContext(
            IGameContext gameContext,
            IPlayerMana mana,
            IGameRound gameRound,
            IPlayerMoves moves,
            ICardDefinition definition)
        {
            _mana = mana;
            _gameRound = gameRound;
            _moves = moves;
            _definition = definition;
            TargetBoard = SelectTargetBoard(definition.Type, gameContext);
        }

        private readonly IPlayerMana _mana;
        private readonly IGameRound _gameRound;
        private readonly IPlayerMoves _moves;
        private readonly ICardDefinition _definition;

        private readonly ViewableProperty<bool> _isAvailable = new();

        public IBoard TargetBoard { get; }
        public IViewableProperty<bool> IsAvailable => _isAvailable;
        public CardType Type => _definition.Type;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _mana.Current.View(lifetime, Recalculate);
            _gameRound.Player.Advise(lifetime, Recalculate);
        }

        private void Recalculate()
        {
            if (_gameRound.IsTurnAllowed == false)
            {
                _isAvailable.Set(false);
                return;
            }

            if (_definition.ManaCost > _mana.Current.Value)
            {
                _isAvailable.Set(false);
                return;
            }

            if (_moves.IsAvailable() == false)
            {
                _isAvailable.Set(false);
                return;
            }

            _isAvailable.Set(true);
        }

        private IBoard SelectTargetBoard(CardType type, IGameContext gameContext)
        {
            return type switch
            {
                CardType.Trebuchet => gameContext.Other.Board,
                CardType.Trebuchet_Max => gameContext.Other.Board,
                CardType.Bloodhound => gameContext.Self.Board,
                CardType.Bloodhound_Max => gameContext.Self.Board,
                CardType.ErosionDozer => gameContext.Self.Board,
                CardType.ErosionDozer_Max => gameContext.Self.Board,
                CardType.ZipZap => gameContext.Self.Board,
                CardType.ZipZap_Max => gameContext.Self.Board,
                CardType.TrebuchetAimer => null,
                CardType.TrebuchetAimer_Max => null,
                CardType.Gravedigger => null,
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}