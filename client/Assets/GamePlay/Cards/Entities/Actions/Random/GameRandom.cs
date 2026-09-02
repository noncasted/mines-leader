using System;
using Cysharp.Threading.Tasks;
using Internal;
using NaughtyAttributes;
using UnityEngine;
using VContainer;

namespace GamePlay.Cards
{
    public interface IGameRandom
    {
        void ResetViews();

        /// <summary>
        /// Position where the dice/coin lands. Used by cards that show
        /// their effect without playing a roll.
        /// </summary>
        Vector2 GetLandingPosition(bool isOwned);

        UniTask<Vector2> PlayCoinFlip(IReadOnlyLifetime lifetime, bool isHeads, bool isOwned);
        UniTask<Vector2> PlayDiceRoll(IReadOnlyLifetime lifetime, int result, bool isOwned);
    }

    [DisallowMultipleComponent]
    public class GameRandom : MonoBehaviour, IGameRandom, ISceneService
    {
        [SerializeField] private Path _ownPath;
        [SerializeField] private Path _opponentPath;

        [SerializeField] private SpriteRenderer _renderer;

        private ILifetime _lifetime;
        private IUpdater _updater;

        [Inject]
        internal void Construct(IUpdater updater)
        {
            _updater = updater;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IGameRandom>();

            _lifetime = this.GetObjectLifetime().Child();
            _renderer.sprite = null;
        }

        public void ResetViews()
        {
            _renderer.gameObject.SetActive(false);
            _lifetime.Terminate();
            _lifetime = this.GetObjectLifetime().Child();
        }

        public Vector2 GetLandingPosition(bool isOwned)
        {
            var path = isOwned == true ? _ownPath : _opponentPath;

            return path.To.position;
        }

        public async UniTask<Vector2> PlayCoinFlip(IReadOnlyLifetime lifetime, bool isHeads, bool isOwned)
        {
            _renderer.sprite = null;
            Vector2 landing;

            if (isOwned == true)
            {
                _renderer.flipX = true;
                landing = await Animate(lifetime.Intersect(_lifetime), _ownPath, Sprites.GameField.GameActionCoinFlip);
            }
            else
            {
                _renderer.flipX = false;
                landing = await Animate(lifetime.Intersect(_lifetime), _opponentPath, Sprites.GameField.GameActionCoinFlip);
            }

            await UniTask.Delay(TimeSpan.FromSeconds(0.4f));

            _renderer.gameObject.SetActive(false);

            return landing;
        }

        public async UniTask<Vector2> PlayDiceRoll(IReadOnlyLifetime lifetime, int result, bool isOwned)
        {
            _renderer.sprite = null;
            Vector2 landing;

            if (isOwned == true)
            {
                _renderer.flipX = true;
                landing = await Animate(lifetime.Intersect(_lifetime), _ownPath, Sprites.GameField.GameActionDiceRoll);
            }
            else
            {
                _renderer.flipX = false;
                landing = await Animate(lifetime.Intersect(_lifetime), _opponentPath, Sprites.GameField.GameActionDiceRoll);
            }

            await UniTask.Delay(TimeSpan.FromSeconds(0.4f));

            _renderer.gameObject.SetActive(false);

            return landing;
        }

        private async UniTask<Vector2> Animate(IReadOnlyLifetime lifetime, Path path, ISpriteAnimationData animation)
        {
            _renderer.gameObject.SetActive(true);
            var startPosition = path.From.position;
            var endPosition = path.To.position;
            var objectTransform = _renderer.transform;
            var duration = animation.Time;
            var time = 0f;

            await _updater.RunUpdateAction(lifetime, () => time < duration, delta => {
                var progress = time / duration;

                var spriteIndex = Mathf.FloorToInt(progress * animation.Sprites.Count);

                if (spriteIndex >= animation.Sprites.Count)
                    spriteIndex = animation.Sprites.Count - 1;

                _renderer.sprite = animation.Sprites[spriteIndex];

                var moveProgress = path.MoveCurve.Evaluate(progress);
                var heightProgress = path.HeightCurve.Evaluate(progress);
                var height = heightProgress * path.MaxHeight;
                var currentPosition = Vector3.Lerp(startPosition, endPosition, moveProgress);
                currentPosition.y += height;
                objectTransform.position = currentPosition;
                time += delta;
            });

            return endPosition;
        }

        [Sirenix.OdinInspector.Button]
        private void DebugDice()
        {
            PlayDiceRoll(this.GetObjectLifetime(), 3, true).Forget();
        }

        [Sirenix.OdinInspector.Button]
        private void DebugCoin()
        {
            PlayCoinFlip(this.GetObjectLifetime(), true, false).Forget();
        }

        [Serializable]
        public class Path
        {
            [SerializeField] private Transform _from;
            [SerializeField] private Transform _to;
            [SerializeField] [CurveRange] private AnimationCurve _moveCurve;
            [SerializeField] [CurveRange] private AnimationCurve _heightCurve;
            [SerializeField] private float _maxHeight;

            public Transform From => _from;
            public Transform To => _to;
            public AnimationCurve MoveCurve => _moveCurve;
            public AnimationCurve HeightCurve => _heightCurve;
            public float MaxHeight => _maxHeight;
        }
    }
}