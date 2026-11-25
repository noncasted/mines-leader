using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace GamePlay.Services
{
    public interface IGameFloatingText
    {
        GameFloatingTextBuilder Create();
        GameFloatingTextBuilder Create(Vector3 position);
        GameFloatingTextBuilder Create(Vector2 position);

        UniTask Run(GameFloatingTextRequest request);
    }

    [DisallowMultipleComponent]
    public class GameFloatingText : MonoBehaviour, IGameFloatingText, ISceneService
    {
        [SerializeField] private float _time = 1f;
        [SerializeField] private float _distance = 1f;
        [SerializeField] private float _scale = 1f;
        [SerializeField] private Vector3 _direction = Vector3.up;
        [SerializeField] [CurveRange] private AnimationCurve _move = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] [CurveRange] private AnimationCurve _fade = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [SerializeField] [CurveRange(0, 0, 1, 2)]
        private AnimationCurve _size = AnimationCurve.Constant(0f, 1f, 1f);

        private IUpdater _updater;
        private PrefabPool<GameFloatingTextView> _pool;

        [Inject]
        public void Construct(IUpdater updater)
        {
            _updater = updater;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IGameFloatingText>();
        }

        public GameFloatingTextBuilder Create()
        {
            var request = new GameFloatingTextRequest
            {
                Position = transform.position,
                Direction = _direction.normalized,
                Time = _time,
                Distance = _distance,
                Scale = _scale,
                Move = _move,
                Fade = _fade,
                Size = _size
            };

            return new GameFloatingTextBuilder(this, request);
        }

        public GameFloatingTextBuilder Create(Vector3 position)
        {
            return Create().At(position);
        }

        public GameFloatingTextBuilder Create(Vector2 position)
        {
            return Create().At(position);
        }

        public async UniTask Run(GameFloatingTextRequest request)
        {
            var lifetime = this.GetObjectLifetime();

            if (request.Delay > 0f)
                await _updater.RunDelay(lifetime, request.Delay);

            if (lifetime.IsTerminated == true)
                return;

            // The catalogue prefab is loaded with the game content, after the scene services are injected.
            if (_pool == null)
                _pool = new PrefabPool<GameFloatingTextView>(lifetime, GamePlayPrefabs.FloatingText, transform);

            var textLifetime = lifetime.Child();

            try
            {
                var instance = _pool.Get(textLifetime);
                instance.Setup(request);

                var direction = request.Direction == Vector3.zero ? Vector3.up : request.Direction;
                var time = request.Time > 0f ? request.Time : _time;

                await _updater.Progression(textLifetime, time, progress => {
                    if (request.Move != null)
                        instance.SetOffset(direction * (request.Distance * request.Move.Evaluate(progress)));

                    if (request.Fade != null)
                        instance.SetAlpha(request.Fade.Evaluate(progress));

                    if (request.Size != null)
                        instance.SetScale(request.Size.Evaluate(progress));
                });
            }
            finally
            {
                textLifetime.Terminate();
            }
        }
    }
}