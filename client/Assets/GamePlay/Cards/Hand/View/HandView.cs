using UnityEngine;

namespace GamePlay.Cards
{
    [DisallowMultipleComponent]
    public class HandView : MonoBehaviour, IHandView
    {
        [SerializeField] private HandPositions _positions;
        
        public HandPositions Positions => _positions;
    }
}