using Internal;
using UnityEngine;

namespace Global.Audio
{
    public class GlobalAudioOptions : EnvAsset
    {
        [SerializeField] private AudioListener _listener;
        [SerializeField] private AudioPlayer _player;
        
        public AudioListener Listener => _listener;
        public AudioPlayer Player => _player;
    }
}