using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardDropOptions
    {
        float Time { get; }
        Vector2 TwistAngleRange { get; }
        Curve MoveCurve { get; }
        Curve TwistCurve { get; }
        Curve ScaleCurve { get; }
    }

    public static class CardDropMotion
    {
        private const string OverlaySortingLayer = "UI";

        public static UniTask Play(
            IUpdater updater,
            IReadOnlyLifetime lifetime,
            ICardTransform transform,
            ICardRenderer renderer,
            CardDropSlot slot,
            float twistAngle,
            ICardDropOptions options,
            Vector2? dropPosition)
        {
            renderer.SetSortingLayer(OverlaySortingLayer);
            renderer.SetSortingOrder(slot.SortingOrder);

            var endPosition = slot.Position;
            var startPosition = transform.Position;
            var startRotation = transform.Rotation;
            var control = dropPosition ?? Vector2.Lerp(startPosition, endPosition, 0.5f);

            var moveCurve = options.MoveCurve.CreateInstance();
            var twistCurve = options.TwistCurve.CreateInstance();
            var scaleCurve = options.ScaleCurve.CreateInstance();

            return updater.RunUpdateAction(lifetime, options.Time, delta => {
                var moveFactor = moveCurve.StepForward(delta);
                var twistFactor = twistCurve.StepForward(delta);
                var scaleFactor = scaleCurve.StepForward(delta);
                var position = EvaluateQuadratic(startPosition, control, endPosition, moveFactor);
                var rotation = Mathf.LerpAngle(startRotation, 0f, moveFactor);
                rotation += twistFactor * twistAngle;

                transform.SetPosition(position);
                transform.SetRotation(rotation);
                transform.SetScale(Vector2.one * scaleFactor);
            });
        }

        private static Vector2 EvaluateQuadratic(Vector2 start, Vector2 control, Vector2 end, float t)
        {
            var oneMinus = 1f - t;
            return oneMinus * oneMinus * start
                   + 2f * oneMinus * t * control
                   + t * t * end;
        }
    }
}
