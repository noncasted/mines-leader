
using UnityEngine;

namespace GamePlay.Cards
{
    public interface IDeckView
    {
        Vector2 PickPoint { get; }
        
        void UpdateAmount(int amount);
    }
}