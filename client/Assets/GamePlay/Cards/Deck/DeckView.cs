using System.Collections.Generic;
using GamePlay.Prefabs;
using Global.Constants;
using Internal;
using Tools;
using UnityEngine;
using VContainer;

namespace GamePlay.Cards
{
    public interface IDeckView
    {
        Vector2 PickPoint { get; }

        void UpdateAmount(int amount);
    }

    [DisallowMultipleComponent]
    public class DeckView : MonoBehaviour, IDeckView, IEntityComponent
    {
        [SerializeField] private float _cardHeight = GameConstants.PixelSize;

        private readonly List<DeckCard> _cards = new();
        private GamePrefabs _prefabs;

        public Vector2 PickPoint => transform.position + Vector3.up * _cardHeight * _cards.Count;

        [Inject]
        internal void Construct(GamePrefabs prefabs)
        {
            _prefabs = prefabs;
        }
        
        public void Register(IEntityBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IDeckView>();

            builder.Register<Deck>()
                   .As<IDeck>();
        }

        public void UpdateAmount(int amount)
        {
            var delta = amount - _cards.Count;

            if (delta > 0)
            {
                for (var i = 0; i < delta; i++)
                {
                    var position = transform.position + Vector3.up * _cardHeight * _cards.Count;
                    var card = Instantiate(_prefabs.DeckCard, position, Quaternion.identity, transform);
                    _cards.Add(card);
                    card.Construct(_cards.Count);
                }
            }
            else if (delta < 0)
            {
                for (var i = 0; i < -delta; i++)
                {
                    var lastIndex = _cards.Count - 1;
                    var lastCard = _cards[lastIndex];
                    _cards.RemoveAt(lastIndex);
                    Destroy(lastCard.gameObject);
                }
            }
        }
    }
}