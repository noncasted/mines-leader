using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardDropped
    {
        UniTask Enter(IReadOnlyLifetime lifetime);
    }

    public class CardDropped : ICardDropped
    {
        public CardDropped(
            IUpdater updater,
            ICardRenderer renderer,
            ICardStateLifetime stateLifetime)
        {
            _updater = updater;
            _renderer = renderer;
            _stateLifetime = stateLifetime;
        }

        private readonly IUpdater _updater;
        private readonly ICardRenderer _renderer;
        private readonly ICardStateLifetime _stateLifetime;

        public UniTask Enter(IReadOnlyLifetime lifetime)
        {
            var options = GamePlayAssets.CardDroppedOptions;

            var stateLifetime = _stateLifetime.OccupyLifetime();

            var startSpritesColor = _renderer.SpritesColor;
            var startNameColor = _renderer.NameTextColor;
            var startDescriptionColor = _renderer.DescriptionTextColor;
            var fadeCurve = options.FadeCurve.CreateInstance();

            return _updater.RunUpdateAction(stateLifetime, options.Time, delta => {
                var factor = fadeCurve.StepForward(delta);

                _renderer.OverrideColors(
                    Color.Lerp(startSpritesColor, options.SpritesColor, factor),
                    Color.Lerp(startNameColor, options.NameColor, factor),
                    Color.Lerp(startDescriptionColor, options.DescriptionColor, factor));
            });
        }
    }
}
