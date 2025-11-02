using Internal;
using UnityEngine;

namespace GamePlay.Boards.Effects
{
    public abstract class CellEffect : MonoBehaviour
    {
        public abstract void Activate(IReadOnlyLifetime lifetime);
    }
}