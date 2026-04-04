using System;
using UnityEngine;

namespace Internal
{
    [Serializable]
    public class DebugOptions
    {
        [SerializeField] private bool _enableGizmos;
        [SerializeField] private bool _enableLogs;

        public bool EnableGizmos
        {
            get => _enableGizmos;
            set => _enableGizmos = value;
        }

        public bool EnableLogs
        {
            get => _enableLogs;
            set => _enableLogs = value;
        }
    }
}