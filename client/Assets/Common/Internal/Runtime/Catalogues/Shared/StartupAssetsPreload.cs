using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IStartupAssetsPreload
    {
        /// <summary>Все группы предзагрузки в памяти.</summary>
        IViewableProperty<bool> IsLoaded { get; }

        void Start();
    }

    /// <summary>
    /// Спрайты реестров меты и общие звуки (музыка, интерфейс) живут всё время приложения и из памяти не выходят, поэтому их никто
    /// не отпускает. Качаются с самого старта параллельно остальной загрузке: старт их не ждёт,
    /// ждёт только мета перед сборкой реестров (см. MetaLoop).
    /// </summary>
    public class StartupAssetsPreload : IStartupAssetsPreload
    {
        private readonly ViewableProperty<bool> _isLoaded = new(false);

        public IViewableProperty<bool> IsLoaded => _isLoaded;

        public void Start()
        {
            Load().Forget();
        }

        private async UniTask Load()
        {
            // Ветка идёт параллельно всей загрузке, поэтому лежит в корне трассы и на стек не встаёт.
            using (var stage = GameProfiler.Detached("Startup assets"))
            {
                await UniTask.WhenAll(
                    Retain(stage, Sprites.CardsIcons),
                    Retain(stage, Sprites.CardBuffs),
                    Retain(stage, Sprites.MenuPlay),
                    Retain(stage, Sprites.Portraits),
                    stage.Measure($"Audio: {GlobalAudio.Group.Name}", GlobalAudio.Group.Retain()));
            }

            // Флаг ставится после закрытия отрезка: за ним синхронно идут реестры меты, а за ними
            // меню, которое закрывает всю трассу.
            _isLoaded.Set(true);
        }

        private static UniTask Retain(IProfilerScope stage, AssetGroup group)
        {
            return stage.Measure($"Sprites: {group.Name}", group.Retain());
        }
    }
}
