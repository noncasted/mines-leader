using Internal;
using UnityEngine;

namespace GamePlay.Cards {
    public interface ICardViewFactory {
        CardScopeEntity Create(CardScopeEntity prefab, Vector2 position);
    }

    public class CardViewFactory : MonoBehaviour, ISceneService, ICardViewFactory {
        private int _counter;

        public void Create(IScopeBuilder builder) {
            builder.RegisterComponent(this)
                .As<ICardViewFactory>();
        }

        public CardScopeEntity Create(CardScopeEntity prefab, Vector2 position) {
            var instance = Instantiate(prefab, position, Quaternion.identity, transform);
            _counter++;
            instance.name = $"{prefab.name}_{_counter}";
            return instance;
        }
    }
}
