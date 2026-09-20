using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardView
    {
        void Destroy();
    }

    public class CardView : ICardView
    {
        public CardView(GameCardBindings bindings)
        {
            _bindings = bindings;
        }

        private readonly GameCardBindings _bindings;

        public void Destroy()
        {
            _bindings.CardScopeEntity.Dispose();

            if (_bindings.GameObject != null)
                Object.Destroy(_bindings.GameObject);
        }
    }
}
