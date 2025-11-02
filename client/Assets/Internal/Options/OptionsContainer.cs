using UnityEngine;
using VContainer;

namespace Internal {
    public class OptionsContainer : ScriptableObject {
        [SerializeField] private AssetsOptions _assets;
        [SerializeField] private DebugOptions _debug;
        [SerializeField] private VersionOptions _version;
        [SerializeField] private BackendOptions _backend;
        [SerializeField] private PlatformOptions _platform;

        public AssetsOptions AssetsOptions => _assets;
        public DebugOptions DebugOptions => _debug;
        public VersionOptions VersionOptions => _version;
        public BackendOptions BackendOptions => _backend;
        public PlatformOptions PlatformOptions => _platform;

        public void Register(IContainerBuilder builder) {
            builder.RegisterInstance(this);
            builder.RegisterInstance(AssetsOptions);
            builder.RegisterInstance(PlatformOptions);
            builder.RegisterInstance(BackendOptions);
            builder.RegisterInstance(DebugOptions);
            builder.RegisterInstance(VersionOptions);
        }
    }
}