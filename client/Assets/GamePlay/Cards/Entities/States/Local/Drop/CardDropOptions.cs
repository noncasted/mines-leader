using Internal;
using NaughtyAttributes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GamePlay.Cards
{
    [InlineEditor]
    public class CardDropOptions : EnvAsset
    {
        [SerializeField] private float _moveDistance;
        [SerializeField] private float _time;

        [SerializeField] [CurveRange] private AnimationCurve _moveCurve;
        [SerializeField] [CurveRange(0, -1, 1, 1)] private AnimationCurve _xScaleCurve;
        
        public float MoveDistance => _moveDistance;
        public float Time => _time;
        public AnimationCurve MoveCurve => _moveCurve;
        public AnimationCurve XScaleCurve => _xScaleCurve;
    }
}