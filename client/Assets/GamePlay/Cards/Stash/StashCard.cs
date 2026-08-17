using Tools;
using UnityEngine;

namespace GamePlay.Cards
{
    [DisallowMultipleComponent]
    public class StashCard : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;

        public void Construct(int index)
        {
            var isEven = index % 2 == 0;
            _renderer.sprite = isEven ? Sprites.Cards.Discard0 : Sprites.Cards.Discard1;
            _renderer.sortingOrder = index;
        }
    }
}