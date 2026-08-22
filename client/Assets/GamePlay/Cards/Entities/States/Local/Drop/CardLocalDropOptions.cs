using Internal;
using NaughtyAttributes;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GamePlay.Cards
{
    [InlineEditor]
    public class CardLocalDropOptions : EnvAsset, ICardDropOptions
    {
        [SerializeField] private float _time;

        [SerializeField] [Sirenix.OdinInspector.MinMaxSlider(0f, 720f)]
        private Vector2 _twistAngleRange = new(350f, 370f);

        [SerializeField] [CurveRange] private AnimationCurve _moveCurve;
        [SerializeField] [CurveRange] private AnimationCurve _twistCurve;

        [SerializeField] [CurveRange(0, 0, 1, 2)]
        private AnimationCurve _scaleCurve;

        public float Time => _time;
        public Vector2 TwistAngleRange => _twistAngleRange;
        public Curve MoveCurve => new(_time, _moveCurve);
        public Curve TwistCurve => new(_time, _twistCurve);
        public Curve ScaleCurve => new(_time, _scaleCurve);
    }
}
