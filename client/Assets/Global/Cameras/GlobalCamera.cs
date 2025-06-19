using Internal;
using UnityEngine;

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
        public Camera Camera { get; private set; }

        public void OnBaseSetup(IReadOnlyLifetime lifetime)
        {
            Camera = GetComponent<Camera>();
        }

        public void Enable()
        {
            gameObject.SetActive(true);
        }

        public void Disable()
        {
            gameObject.SetActive(false);
        }
    }
}