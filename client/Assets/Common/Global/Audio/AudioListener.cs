using Internal;
using UnityEngine;

namespace Global.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioListener))]
    public class AudioListener :
        MonoBehaviour,
        IAudioListener,
        IScopeBaseSetup
    {
        private UnityEngine.AudioListener _listener;

        public void OnBaseSetup(IReadOnlyLifetime lifetime)
        {
            _listener = GetComponent<UnityEngine.AudioListener>();
            Enable();
        }

        public void Enable()
        {
            _listener.enabled = true;
        }

        public void Disable()
        {
            _listener.enabled = false;
        }
    }
}