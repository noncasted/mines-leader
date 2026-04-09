using System.Collections.Generic;
using Global.Constants;
using Internal;
using Tools;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface IStashView
    {
        Vector2 PickPoint { get; }

        void UpdateAmount(int amount);
    }

    [DisallowMultipleComponent]
    public class StashView : MonoBehaviour, IStashView, IEntityComponent
    {
        [SerializeField] private float _cardHeight = GameConstants.PixelSize;

        private readonly List<StashCard> _cards = new();

        public Vector2 PickPoint => transform.position + Vector3.up * _cardHeight * _cards.Count;

        public void Register(IEntityBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IStashView>();

            builder.Register<Stash>()
                   .As<IScopeLoaded>()
                   .As<IStash>()
                   .AsSelfResolvable();
        }

        public void UpdateAmount(int amount)
        {
            var delta = amount - _cards.Count;

            if (delta > 0)
            {
                for (var i = 0; i < delta; i++)
                {
                    var position = transform.position + Vector3.up * _cardHeight * _cards.Count;
                    var card = Instantiate(Prefabs.StashCard.As<StashCard>(), position, Quaternion.identity, transform);
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