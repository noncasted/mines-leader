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
        UniTask PlayCoinFlip(IReadOnlyLifetime lifetime, bool isHeads, bool isOwned);
        UniTask PlayDiceRoll(IReadOnlyLifetime lifetime, int result, bool isOwned);
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
        }

        public void ResetViews()
        {
            _renderer.gameObject.SetActive(false);
            _lifetime.Terminate();
            _lifetime = this.GetObjectLifetime().Child();
        }

        public async UniTask PlayCoinFlip(IReadOnlyLifetime lifetime, bool isHeads, bool isOwned)
        {
            if (isOwned == true)
            {
                _renderer.flipX = true;
                await Animate(lifetime.Intersect(_lifetime), _ownPath, Sprites.GameField.GameActionCoinFlip);
            }
            else
            {
                _renderer.flipX = false;
                await Animate(lifetime.Intersect(_lifetime), _opponentPath, Sprites.GameField.GameActionCoinFlip);
            }

            _renderer.gameObject.SetActive(false);
        }

        public async UniTask PlayDiceRoll(IReadOnlyLifetime lifetime, int result, bool isOwned)
        {
            if (isOwned == true)
            {
                _renderer.flipX = true;
                await Animate(lifetime.Intersect(_lifetime), _ownPath, Sprites.GameField.GameActionDiceRoll);
            }
            else
            {
                _renderer.flipX = false;
                await Animate(lifetime.Intersect(_lifetime), _opponentPath, Sprites.GameField.GameActionDiceRoll);
            }

            _renderer.gameObject.SetActive(false);
        }

        private async UniTask Animate(IReadOnlyLifetime lifetime, Path path, ISpriteAnimationData animation)
        {
            _renderer.gameObject.SetActive(true);
            var startPosition = path.From.position;
            var endPosition = path.To.position;
            var objectTransform = _renderer.transform;
            var duration = animation.Time;
            var time = 0f;

            await _updater.RunUpdateAction(lifetime, delta => {
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