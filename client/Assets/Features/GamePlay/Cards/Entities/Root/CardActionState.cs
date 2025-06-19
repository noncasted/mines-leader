using GamePlay.Loop;
using GamePlay.Players;
using Internal;
using Meta;

namespace GamePlay.Cards
{
    public interface ICardActionState
    {
        IViewableProperty<bool> IsAvailable { get; }
    }
    
    public class CardActionState : ICardActionState, IScopeSetup
    {
        public CardActionState(
            IPlayerMana mana, 
            IGameRound gameRound,
            IPlayerTurns turns,
            ICardDefinition definition)
        {
            _mana = mana;
            _gameRound = gameRound;
            _turns = turns;
            _definition = definition;
        }

        private readonly IPlayerMana _mana;
        private readonly IGameRound _gameRound;
        private readonly IPlayerTurns _turns;
        private readonly ICardDefinition _definition;
        
        private readonly ViewableProperty<bool> _isAvailable = new();

        public IViewableProperty<bool> IsAvailable => _isAvailable;

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
            
            if (_turns.IsAvailable() == false)
            {
                _isAvailable.Set(false);
                return;
            }

            _isAvailable.Set(true);
        }
    }
}