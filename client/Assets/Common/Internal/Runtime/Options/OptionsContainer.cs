using UnityEngine;

namespace Internal
{
    // Настройки проекта живут ассетом каталога: правятся в Project Tools,
    // в билд попадают вместе с каталогом и достаются как InternalAssets.OptionsContainer.
    public class OptionsContainer : EnvAsset
    {
        [SerializeField] private DebugOptions _debug = new();
        [SerializeField] private VersionOptions _version = new();
        [SerializeField] private BackendOptions _backend = new();
        [SerializeField] private PlatformOptions _platform = new();

        public DebugOptions DebugOptions => _debug;
        public VersionOptions VersionOptions => _version;
        public BackendOptions BackendOptions => _backend;
        public PlatformOptions PlatformOptions => _platform;

        public void Register(IBuilder builder)
        {
            builder.RegisterInstance(this);
            builder.RegisterInstance(PlatformOptions);
            builder.RegisterInstance(BackendOptions);
            builder.RegisterInstance(DebugOptions);
            builder.RegisterInstance(VersionOptions);
        }
    }
}