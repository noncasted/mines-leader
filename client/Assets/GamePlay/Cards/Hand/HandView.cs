using UnityEngine;

namespace GamePlay.Cards
{
    public interface IHandView
    {
        HandPositions Positions { get; }
    }
    
    [DisallowMultipleComponent]
    public class HandView : MonoBehaviour, IHandView
    {
        [SerializeField] private HandPositions _positions;
        
        public HandPositions Positions => _positions;
    }
}