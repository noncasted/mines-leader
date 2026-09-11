using Global.Cameras;
using Internal;
using UnityEngine;

namespace GamePlay.Services
{
    public interface IGameInput
    {
        IViewableProperty<bool> Flag { get; }
        IViewableProperty<bool> Open { get; }
        IViewableDelegate Cheats { get; }

        Vector2 World { get; }
        Vector2 Screen { get; }
    }

    public class GameInput : IGameInput, IScopeSetup, IUpdatable
    {
        public GameInput(
            IUpdater updater,
            ICameraUtils cameraUtils)
        {
            _updater = updater;
            _cameraUtils = cameraUtils;
        }

        private readonly ViewableProperty<bool> _flag = new();
        private readonly ViewableProperty<bool> _open = new();
        private readonly ViewableDelegate _cheats = new();

        private readonly IUpdater _updater;
        private readonly ICameraUtils _cameraUtils;

        private Vector2 _world;
        private Vector2 _screen;

        public IViewableProperty<bool> Flag => _flag;
        public IViewableProperty<bool> Open => _open;

        public IViewableDelegate Cheats => _cheats;

        public Vector2 World => _world;
        public Vector2 Screen => _screen;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _updater.Add(lifetime, this);
        }

        public void OnUpdate(float delta)
        {
            _flag.Set(Input.GetMouseButton(1));
            _open.Set(Input.GetMouseButton(0));

            if (Input.GetKeyDown(KeyCode.BackQuote) == true)
                _cheats.Invoke();

            Vector2 screenPosition = Input.mousePosition;
            _screen = screenPosition;
            _world = _cameraUtils.ScreenToWorld(screenPosition);
        }
    }
}
