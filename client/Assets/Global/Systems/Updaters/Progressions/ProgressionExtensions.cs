using System;
using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace Global.Systems
{
    public static class ProgressionExtensions
    {
        public static IProgression CreateProgression(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            float time,
            Action<float> callback,
            ProgressionLoop loop = ProgressionLoop.Frame)
        {
            return loop switch
            {
                ProgressionLoop.Frame => new UpdateProgression(lifetime, updater, time, callback),
                ProgressionLoop.Fixed => new FixedProgression(lifetime, updater, time, callback),
                _ => throw new ArgumentOutOfRangeException(nameof(loop), loop, null)
            };
        }

        public static async UniTask Progression(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            float time,
            Action<float> callback,
            ProgressionLoop loop = ProgressionLoop.Frame)
        {
            var handle = updater.CreateProgression(lifetime, time, callback, loop);
            await handle.Process();
        }
        
        public static UniTask CurveProgression(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            AnimationCurve curve,
            float time,
            Action<float> callback)
        {
            return updater.Progression(lifetime, time, Callback);

            void Callback(float progress)
            {
                var factor = curve.Evaluate(progress);
                callback?.Invoke(factor);
            }
        }

        public static UniTask CurveProgression(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            ICurveDefinition curve,
            Action<float> callback)
        {
            return updater.CurveProgression(lifetime, curve.Animation, curve.Time, callback);
        }

        public static UniTask CurveDeltaProgression(
            this IUpdater updater,
            IReadOnlyLifetime lifetime,
            ICurveDefinition curve,
            ProgressionLoop loop,
            Action<float> callback)
        {
            var previousEvaluation = 0f;
            return updater.Progression(lifetime, curve.Time, Callback, loop);

            void Callback(float progress)
            {
                var evaluation = curve.Evaluate(progress);
                var delta = evaluation - previousEvaluation;
                previousEvaluation = evaluation;
                callback?.Invoke(delta);
            }
        }
    }
}