using Cysharp.Threading.Tasks;
using Global.Systems;
using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardStashOptions
    {
        float Time { get; }
        Curve MoveCurve { get; }
        Curve ScaleCurve { get; }
    }

    public static class CardStashMotion
    {
        public static UniTask Play(
            IUpdater updater,
            IReadOnlyLifetime lifetime,
            ICardTransform transform,
            Vector2 endPosition,
            ICardStashOptions options)
        {
            var startPosition = transform.Position;
            var startScale = transform.Scale;
            var startRotation = transform.Rotation;
            var moveCurve = options.MoveCurve.CreateInstance();
            var scaleCurve = options.ScaleCurve.CreateInstance();

            return updater.RunUpdateAction(lifetime, options.Time, delta => {
                var moveFactor = moveCurve.StepForward(delta);
                var scaleFactor = scaleCurve.StepForward(delta);
                var position = Vector2.Lerp(startPosition, endPosition, moveFactor);

                transform.SetPosition(position);
                transform.SetRotation(startRotation);
                transform.SetScale(startScale * scaleFactor);
            });
        }
    }
}
