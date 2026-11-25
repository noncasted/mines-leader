using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IStartupAssetsPreload
    {
        /// <summary>All preload groups in memory.</summary>
        IViewableProperty<bool> IsLoaded { get; }

        /// <summary>Music in memory: loaded separately (its own Music bundle), does not delay the meta or the menu.</summary>
        IViewableProperty<bool> IsMusicLoaded { get; }

        void Start();
    }

    /// <summary>
    /// Meta registry sprites, interface sounds and music live for the whole app lifetime and never leave memory, so nobody
    /// releases them. They download from the very start in parallel with the rest of the loading: the startup does not wait,
    /// the meta waits for sprites and sounds before building the registries (see MetaLoop), nobody waits for the music - the menu starts it when it
    /// has loaded (see MenuLoop).
    /// </summary>
    public class StartupAssetsPreload : IStartupAssetsPreload
    {
        private readonly ViewableProperty<bool> _isLoaded = new(false);
        private readonly ViewableProperty<bool> _isMusicLoaded = new(false);

        public IViewableProperty<bool> IsLoaded => _isLoaded;
        public IViewableProperty<bool> IsMusicLoaded => _isMusicLoaded;

        public void Start()
        {
            Load().Forget();
            LoadMusic().Forget();
        }

        private async UniTask Load()
        {
            // The branch runs in parallel with the whole loading, so it sits at the trace root and does not go on the stack.
            using (var stage = GameProfiler.Detached("Startup assets"))
            {
                UniTask loads;

                // The loads of the bundles are created synchronously: under the ambient they nest into the branch, not the stage.
                using (GameProfiler.Ambient(stage))
                {
                    loads = UniTask.WhenAll(
                        Retain(stage, Sprites.CardsIcons),
                        Retain(stage, Sprites.CardBuffs),
                        Retain(stage, Sprites.MenuPlay),
                        Retain(stage, Sprites.Portraits),
                        stage.Measure($"Audio: {GlobalAudio.Group.Name}", GlobalAudio.Group.Retain()));
                }

                await loads;
            }

            // The flag is set after the span is closed: the meta registries follow synchronously, and after them
            // the menu, which closes the whole trace.
            _isLoaded.Set(true);
        }

        // The music is the biggest asset of the game: a group of its own, so it does not hold back the sprites and the sounds
        // the meta waits for.
        private async UniTask LoadMusic()
        {
            // Nothing waits for it (the menu plays it when it is ready): a root of the trace of its own, not a part of the
            // start; the load of the bundle is created under its ambient to nest into it.
            using (var stage = GameProfiler.Background("Music"))
            {
                UniTask retain;

                using (GameProfiler.Ambient(stage))
                    retain = stage.Measure($"Audio: {MusicAudio.Group.Name}", MusicAudio.Group.Retain());

                await retain;
            }

            _isMusicLoaded.Set(true);
        }

        private static UniTask Retain(IProfilerScope stage, AssetGroup group)
        {
            return stage.Measure($"Sprites: {group.Name}", group.Retain());
        }
    }
}
