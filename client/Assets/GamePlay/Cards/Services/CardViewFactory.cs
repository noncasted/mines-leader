using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardViewFactory
    {
        T Create<T>(T prefab, Vector2 position) where T : CardScopeEntity;
    }

    public class CardViewFactory : MonoBehaviour, ISceneService, ICardViewFactory
    {
        private int _counter;

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<ICardViewFactory>();
        }

        public T Create<T>(T prefab, Vector2 position) where T : CardScopeEntity
        {
            var instance = Instantiate(prefab, position, Quaternion.identity, transform);
            _counter++;
            instance.name = $"{prefab.name}_{_counter}";
            return instance;
        }
    }
}