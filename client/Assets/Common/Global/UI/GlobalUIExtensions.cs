using Internal;

namespace Global.UI
{
    public static class GlobalUIExtensions
    {
        public static IScopeBuilder AddUI(this IScopeBuilder builder)
        {
            builder.Register<UIStateMachine>()
                   .WithScopeLifetime()
                   .As<IUIStateMachine>();

            AddLoadingScreen(builder);

            return builder;
        }

        private static void AddLoadingScreen(IScopeBuilder builder)
        {
            // В вебе экран загрузки живёт в index.html: он же закрывает скачивание билда.
            // Выбор через #if, а не if: генератор контейнера строит все ветки installer'а сразу,
            // и незарегистрированный в рантайме префаб или альтернатива роняют создание скоупа.
#if UNITY_WEBGL && !UNITY_EDITOR
            builder.Register<WebLoadingScreen>()
                   .As<ILoadingScreen>();
#else
            var loadingScreen = builder.Instantiate(GlobalPrefabs.LoadingScreen);

            builder.Inject(loadingScreen);

            builder.RegisterInstance(loadingScreen)
                   .As<ILoadingScreen>()
                   .As<IScopeSetup>();
#endif
        }
    }
}