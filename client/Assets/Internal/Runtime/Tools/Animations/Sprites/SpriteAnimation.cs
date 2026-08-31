using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    public class SpriteAnimation : IAnimation, IUpdatable, IScopeSetup
    {
        public SpriteAnimation(
            IUpdater updater,
            ISpriteAnimationRenderer renderer,
            float defaultTime,
            Color color,
            IFrameProvider frameProvider)
        {
            _updater = updater;
            _renderer = renderer;
            _defaultTime = defaultTime;
            _color = color;
            _void = new SpriteAnimationVoidUpdatable(renderer, frameProvider);
        }

        private readonly IUpdater _updater;
        private readonly ISpriteAnimationRenderer _renderer;
        private readonly float _defaultTime;
        private readonly Color _color;
        private readonly SpriteAnimationVoidUpdatable _void;

        private SpriteAnimationLoopUpdatable _loop;
        private SpriteAnimationAsyncUpdatable _async;

        private IUpdatableSpriteAnimation _current;
        private IReadOnlyLifetime _lifetime;

        public bool IsPlaying => _current != null;
        public IUpdatableSpriteAnimation Current => _current;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _updater.Add(lifetime, this);
        }

        public void Play(IReadOnlyLifetime lifetime, float time = 0)
        {
            _current?.Dispose();
            _lifetime = lifetime;

            if (Mathf.Approximately(time, 0f) == true)
                time = _defaultTime;

            _renderer.SetColor(_color);
            _void.Start(time);
            _current = _void;
        }

        public async UniTask PlayAsync(IReadOnlyLifetime lifetime, float time = 0)
        {
            _current?.Dispose();
            _async ??= new SpriteAnimationAsyncUpdatable(_void);
            _lifetime = lifetime;

            if (Mathf.Approximately(time, 0f) == true)
                time = _defaultTime;

            _renderer.SetColor(_color);
            _current = _async;
            await _async.Process(time);
            _current = null;
        }

        public void PlayLooped(IReadOnlyLifetime lifetime, float time = 0)
        {
            _current?.Dispose();
            _loop ??= new SpriteAnimationLoopUpdatable(_void);
            _lifetime = lifetime;

            if (Mathf.Approximately(time, 0f) == true)
                time = _defaultTime;

            _renderer.SetColor(_color);
            _loop.Start(time);
            _current = _loop;
        }

        public void ManualStop()
        {
            _current?.Dispose();
            _current = null;
        }

        public void OnUpdate(float delta)
        {
            if (_current == null)
                return;

            if (_lifetime.IsTerminated == true)
            {
                _current?.Dispose();
                _current = null;
                return;
            }

            var delete = _current.Update(delta);

            if (delete == true)
                _current = null;
        }
    }
}
