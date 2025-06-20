using Internal;
using NaughtyAttributes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GamePlay.Cards
{
    [InlineEditor]
    public class CardLocalSpawnOptions : EnvAsset
    {
        [SerializeField] [CurveRange] private AnimationCurve _moveCurve;
        [SerializeField] [CurveRange] private AnimationCurve _heightCurve;
        [SerializeField] [CurveRange] private AnimationCurve _rotationCurve;
        
        [SerializeField] private float _time;
        [SerializeField] private float _addHeight;

        public Curve MoveCurve => new(_time, _moveCurve);
        public Curve HeightCurve => new(_time, _heightCurve);
        public float Time => _time;
        public float AddHeight => _addHeight;
        public Curve RotationCurve => new(_time, _rotationCurve);
    }
}