using System;
using UnityEngine;

namespace Internal
{
    [Serializable]
    public class PlatformOptions
    {
        [SerializeField] private PlatformType _platform;

        public PlatformType PlatformType => _platform;
        public bool IsMobile => Application.isMobilePlatform;

        public bool IsEditor
        {
            get
            {
#if UNITY_EDITOR
                return true;
#endif
                return false;
            }
        }
    }
}