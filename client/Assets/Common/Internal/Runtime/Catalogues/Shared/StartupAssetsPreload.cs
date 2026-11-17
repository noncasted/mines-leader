using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IStartupAssetsPreload
    {
        /// <summary>Все группы предзагрузки в памяти.</summary>
        IViewableProperty<bool> IsLoaded { get; }

        /// <summary>Музыка в памяти: грузится отдельно (свой бандл Music), мету и меню не задерживает.</summary>
        IViewableProperty<bool> IsMusicLoaded { get; }

        void Start();
    }

    /// <summary>
    /// Спрайты реестров меты, звуки интерфейса и музыка живут всё время приложения и из памяти не выходят, поэтому их никто
    /// не отпускает. Качаются с самого старта параллельно остальной загрузке: старт их не ждёт,
    /// мета ждёт спрайты и звуки перед сборкой реестров (см. MetaLoop), музыку не ждёт никто - меню включает её, когда она
    /// загрузится (см. MenuLoop).
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
            // Ветка идёт параллельно всей загрузке, поэтому лежит в корне трассы и на стек не встаёт.
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

            // Флаг ставится после закрытия отрезка: за ним синхронно идут реестры меты, а за ними
            // меню, которое закрывает всю трассу.
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
