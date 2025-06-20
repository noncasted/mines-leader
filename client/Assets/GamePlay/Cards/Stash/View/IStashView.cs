using UnityEngine;

namespace GamePlay.Cards
{
    public interface IStashView
    {
        Vector2 PickPoint { get; }
        
        void UpdateAmount(int amount);
    }
}