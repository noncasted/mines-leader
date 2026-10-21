using Internal;
using UnityEngine;

namespace Global.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioListener))]
    public class AudioListener :
        MonoBehaviour,
        IScopeBaseSetup
    {
        private UnityEngine.AudioListener _listener;

        public void OnBaseSetup(IReadOnlyLifetime lifetime)
        {
            _listener = GetComponent<UnityEngine.AudioListener>();
            _listener.enabled = true;
        }
    }
}