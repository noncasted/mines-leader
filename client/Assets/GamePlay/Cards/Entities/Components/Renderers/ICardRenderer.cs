using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardRenderer
    {
        void SetSortingOrder(int order);
        void SetAllColor(Color color);
    }
}