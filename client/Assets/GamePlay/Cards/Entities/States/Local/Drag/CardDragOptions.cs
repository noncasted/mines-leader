using Internal;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GamePlay.Cards
{
    [InlineEditor]
    public class CardDragOptions : EnvAsset
    {
        [SerializeField] private Curve _transitionCurve;
        [SerializeField] private float _scale = 2f;
        [SerializeField] private float _rotation;
        [SerializeField] private float _handForce;
        [SerializeField] private float _maxForceDistance;
        [SerializeField] private float _moveDistance;

        public Curve TransitionCurve => _transitionCurve;
        public float Scale => _scale;
        public float Rotation => _rotation;
        public float HandForce => _handForce;
        public float MaxForceDistance => _maxForceDistance;
        public float MoveDistance => _moveDistance;
    }
}