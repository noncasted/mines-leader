using Internal;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GamePlay.Cards
{
    [InlineEditor]
    public class CardDragOptions : EnvAsset
    {
        [SerializeField] private float _handForce;
        [SerializeField] private float _maxForceDistance;
        [SerializeField] private float _moveDistance;
        
        public float HandForce => _handForce;
        public float MaxForceDistance => _maxForceDistance;
        public float MoveDistance => _moveDistance;
    }
}