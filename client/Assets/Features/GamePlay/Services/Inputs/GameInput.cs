using Global.Cameras;
using Global.Inputs;
using Global.Systems;
using Internal;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GamePlay.Services
{
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
        
        private readonly IUpdater _updater;
        private readonly ICameraUtils _cameraUtils;
        private readonly IUserInput _localUser;
        
        private Vector2 _world;
        private Vector2 _screen;

        public IViewableProperty<bool> Flag => _flag;
        public IViewableProperty<bool> Open => _open;
        public Vector2 World => _world;
        public Vector2 Screen => _screen;
        
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _updater.Add(lifetime, this);
            
            var controls = _localUser.Controls.GamePlay;
            controls.Flag.AttachFlag(lifetime, _flag);
            controls.Open.AttachFlag(lifetime, _open);
        }

        public void OnUpdate(float delta)
        {
            var screenPosition = Mouse.current.position.ReadValue();
            _screen = screenPosition;
            _world = _cameraUtils.ScreenToWorld(screenPosition);
        }
    }
}