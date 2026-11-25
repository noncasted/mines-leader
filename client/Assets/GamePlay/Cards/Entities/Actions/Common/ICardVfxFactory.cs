using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardVfxFactory
    {
        // The instance comes from a pool and goes back to it when the lifetime ends.
        T Create<T>(IReadOnlyLifetime lifetime, T prefab, Vector2 position, float angle = 0) where T : MonoBehaviour;
    }
}
