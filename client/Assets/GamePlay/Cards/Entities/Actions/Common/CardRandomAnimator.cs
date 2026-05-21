using Animations;
using Cysharp.Threading.Tasks;
using Global.Systems;
using Internal;
using UnityEngine;
using VContainer;
using VContainer.Internal;

namespace GamePlay.Cards
{
    public interface ICardRandomAnimator
    {
        UniTask PlayCoinFlip(IReadOnlyLifetime lifetime, bool isHeads);
        UniTask PlayDiceRoll(IReadOnlyLifetime lifetime, int result);
        void Reset();
    }

    [DisallowMultipleComponent]
    public class CardRandomAnimator : MonoBehaviour, ISceneService, ICardRandomAnimator, ISpriteAnimationRenderer
    {
        [SerializeField] private SpriteRenderer _renderer;

        [SerializeField] private ForwardAnimationAsset _coin;
        [SerializeField] private Sprite _coinHeads;
        [SerializeField] private Sprite _coinTails;

        [SerializeField] private ForwardAnimationAsset _dice;
        [SerializeField] private SerializableDictionary<int, Sprite> _diceResults;

        private ForwardSpriteAnimation _coinAnimation;
        private ForwardSpriteAnimation _diceAnimation;

        [Inject]
        private void Construct(IUpdater updater)
        {
            _coinAnimation = CreateAnimation(_coin);
            _diceAnimation = CreateAnimation(_dice);

            gameObject.SetActive(false);

            return;

            ForwardSpriteAnimation CreateAnimation(ForwardAnimationAsset data)
            {
                return new ForwardSpriteAnimation(
                    new ForwardSpriteAnimation.Utils(updater, new ContainerLocal<ISpriteAnimationRenderer>(this)),
                    new SpriteAnimationData(data.Sprites, data.Time, data.Color));
            }
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<ICardRandomAnimator>()
                   .AsSelfResolvable();
        }

        public async UniTask PlayCoinFlip(IReadOnlyLifetime lifetime, bool isHeads)
        {
            gameObject.SetActive(true);

            var animationLifetime = lifetime.Child();
            _coinAnimation.OnSetup(animationLifetime);
            await _coinAnimation.PlayAsync(animationLifetime);
            animationLifetime.Terminate();

            _renderer.sprite = isHeads ? _coinHeads : _coinTails;
            await UniTask.Delay(500, cancellationToken: lifetime.Token);

            gameObject.SetActive(false);
        }

        public async UniTask PlayDiceRoll(IReadOnlyLifetime lifetime, int result)
        {
            gameObject.SetActive(true);

            var animationLifetime = lifetime.Child();
            _diceAnimation.OnSetup(animationLifetime);
            await _diceAnimation.PlayAsync(animationLifetime);
            animationLifetime.Terminate();

            if (_diceResults.TryGetValue(result, out var sprite))
                _renderer.sprite = sprite;

            await UniTask.Delay(500, cancellationToken: lifetime.Token);

            gameObject.SetActive(false);
        }

        public void Reset()
        {
            _renderer.sprite = null;
        }

        public void SetSprite(Sprite sprite)
        {
            _renderer.sprite = sprite;
        }

        public void SetColor(Color color)
        {
            _renderer.color = color;
        }
    }
}