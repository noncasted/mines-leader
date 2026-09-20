using Internal;
using NaughtyAttributes;
using UnityEngine;

namespace GamePlay.Cards
{
    public class CardLocalStashOptions : EnvAsset, ICardStashOptions
    {
        [SerializeField] private float _time = 0.6f;

        [SerializeField] [CurveRange] private AnimationCurve _moveCurve;
        [SerializeField] [CurveRange(0, 0, 1, 2)] private AnimationCurve _scaleCurve;

        public float Time => _time;
        public Curve MoveCurve => new(_time, _moveCurve);
        public Curve ScaleCurve => new(_time, _scaleCurve);
    }
}
