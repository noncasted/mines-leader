using UnityEngine;

namespace GamePlay.Cards
{
    [DisallowMultipleComponent]
    public class StashCard : MonoBehaviour
    {
        [SerializeField] private Sprite _even;
        [SerializeField] private Sprite _odd;

        [SerializeField] private SpriteRenderer _renderer;
        
        public void Construct(int index)
        {
            var isEven = index % 2 == 0;
            _renderer.sprite = isEven ? _even : _odd;
            _renderer.sortingOrder = index;
        }
    }

}