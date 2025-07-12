using Cysharp.Threading.Tasks;
using Global.Systems;
using Internal;
using UnityEngine;
using VContainer;

namespace GamePlay.Services
{
    public interface IGameCamera
    {
        Camera Camera { get; }

        void Shake(float time, float intensity);
    }

    [DisallowMultipleComponent]
    public class GameCamera : MonoBehaviour, IGameCamera, ISceneService, IScopeSetup, IUpdatable
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _shakeInterval = 0.05f;

        private IUpdater _updater;

        private float _shakeTimer;
        private float _currentShakeTime;
        private float _currentShakeIntensity;
        private Vector3 _cameraOrigin;

        public Camera Camera => _camera;

        [Inject]
        private void Construct(IUpdater updater)
        {
            _updater = updater;
            _cameraOrigin = _camera.transform.position;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IGameCamera>()
                .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _updater.Add(lifetime, this);
        }

        public void SetSize(float size, float time)
        {
            var start = _camera.orthographicSize;

            _updater.Progression(this.GetObjectLifetime(), time, progress =>
            {
                var newSize = Mathf.Lerp(start, size, progress);
                _camera.orthographicSize = newSize;
            }).Forget();
        }

        public void Shake(float time, float intensity)
        {
            _currentShakeTime = time;
            _currentShakeIntensity = intensity;
        }

        public void OnUpdate(float delta)
        {
            if (_currentShakeTime <= 0)
            {
                _camera.transform.position = _cameraOrigin;
                return;
            }

            _shakeTimer += delta;
            _currentShakeTime -= delta;

            if (_shakeTimer < _shakeInterval)
                return;

            _shakeTimer = 0f;

            var offsetX = Random.Range(-_currentShakeIntensity, _currentShakeIntensity);
            var offsetY = Random.Range(-_currentShakeIntensity, _currentShakeIntensity);

            var position = _cameraOrigin + new Vector3(offsetX, offsetY, 0f);
            _camera.transform.position = new Vector3(position.x, position.y, _camera.transform.position.z);
        }
    }

    public static class GameCameraExtensions
    {
        public static void BaseShake(this IGameCamera camera)
        {
            camera.Shake(0.2f, 0.1f);
        }
    }
}