using NaughtyAttributes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GamePlay.Cards
{
    [InlineEditor]
    public class HandPositionsOptions : ScriptableObject
    {
        [SerializeField] [CurveRange] private AnimationCurve _evaluationCurve;
        [SerializeField] [CurveRange(-1, -1, 2, 2)] private AnimationCurve _forceCurve;

        [SerializeField] [CurveRange(-1, -1, 2, 2)] private AnimationCurve _xCurve;
        [SerializeField] [CurveRange(-1, -1, 2, 2)] private AnimationCurve _yCurve;
        [SerializeField] [CurveRange(-1, -2, 2, 3)] private AnimationCurve _rotationCurve;
        [SerializeField] [CurveRange(0, 0, 10, 1)] private AnimationCurve _xSizeCurve;
        
        [SerializeField] [Min(0f)] private float _cardXSize;
        [SerializeField] [Min(0f)] private float _magnitude;
        [SerializeField] private float _angeRange;
        [SerializeField] [Min(0f)] private float _moveSpeed;
        
        public AnimationCurve EvaluationCurve => _evaluationCurve;
        public AnimationCurve ForceCurve => _forceCurve;

        public AnimationCurve XCurve => _xCurve;
        public AnimationCurve YCurve => _yCurve;
        public AnimationCurve RotationCurve => _rotationCurve;
        public AnimationCurve XSizeCurve => _xSizeCurve; 
        
        public float CardXSize => _cardXSize;
        public float Magnitude => _magnitude;
        public float AngleRange => _angeRange;
        public float MoveSpeed => _moveSpeed;
    }
}