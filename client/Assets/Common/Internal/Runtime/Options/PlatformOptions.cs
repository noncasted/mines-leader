using System;
using UnityEngine;

namespace Internal
{
    [Serializable]
    public class PlatformOptions
    {
        [SerializeField] private PlatformType _platform = PlatformType.Website;

        public PlatformType PlatformType
        {
            get => _platform;
            set => _platform = value;
        }

        public bool IsMobile => Application.isMobilePlatform;

        public bool IsEditor
        {
            get
            {
#if UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }
    }
}