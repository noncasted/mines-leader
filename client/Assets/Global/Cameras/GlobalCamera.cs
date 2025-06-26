using Internal;
using UnityEngine;
using VContainer;

namespace Global.Cameras
{
    public interface IGlobalCamera
    {
        Camera Camera { get; }
        void Enable();
        void Disable();
    }
    
    [DisallowMultipleComponent]
    public class GlobalCamera : MonoBehaviour, IGlobalCamera, IScopeBaseSetup
    {
        private ICurrentCamera _currentCamera;
        
        public Camera Camera { get; private set; }
        
        [Inject] 
        private void Construct(ICurrentCamera currentCamera)
        {
            _currentCamera = currentCamera;
        }

        public void OnBaseSetup(IReadOnlyLifetime lifetime)
        {
            Camera = GetComponent<Camera>();
        }

        public void Enable()
        {
            gameObject.SetActive(true);
            _currentCamera.SetCamera(Camera);
        }

        public void Disable()
        {
            gameObject.SetActive(false);
        }
    }
}