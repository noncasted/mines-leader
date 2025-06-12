using System;
using NaughtyAttributes;
using UnityEngine;

namespace Internal
{
    public interface ICurveDefinition
    {
        float Time { get; }
        AnimationCurve Animation { get; }

        CurveInstance CreateInstance();
    }

    [Serializable]
    public class Curve : ICurveDefinition
    {
        public Curve(float time, AnimationCurve curve)
        {
            _time = time;
            _curve = curve;
        }

        [SerializeField] [Min(0f)] private float _time;
        [SerializeField] [CurveRange] private AnimationCurve _curve;

        public float Time => _time;
        public AnimationCurve Animation => _curve;

        public CurveInstance CreateInstance()
        {
            return new CurveInstance(this);
        }
    }

    public struct CurveInstance
    {
        public CurveInstance(ICurveDefinition curve)
        {
            Curve = curve;
            _timer = 0f;
            _progress = 0f;
        }

        private float _timer;
        private float _progress;

        public readonly ICurveDefinition Curve;

        public bool IsFinished => Mathf.Approximately(_timer / Curve.Time, 1f);
        public float Progress => _progress;

        public float StepForward(float delta)
        {
            _timer += delta;
            _timer = Mathf.Clamp(_timer, 0f, Curve.Time);

            _progress = _timer / Curve.Time;

            if (_progress > 1f)
                _progress = 1f;

            return Curve.Evaluate(_progress);
        }

        public float StepBack(float delta)
        {
            _timer -= delta;
            _timer = Mathf.Clamp(_timer, 0f, Curve.Time);

            _progress = _timer / Curve.Time;

            if (_progress > 1f)
                _progress = 1f;

            return Curve.Evaluate(_progress);
        }
    }

    public static class CurveExtensions
    {
        public static float Evaluate(this ICurveDefinition curve, float progress)
        {
            return curve.Animation.Evaluate(progress);
        }
    }
}