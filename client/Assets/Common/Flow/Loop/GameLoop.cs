using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Global.Cameras;
using Global.Telemetry;
using Global.UI;
using Internal;
using Menu.Common;
using Meta;
using Shared;

namespace Flow.Loop
{
    public class GameLoop
    {
        public GameLoop(
            IMenuLoader menuLoader,
            IGamePlayLoader gamePlayLoader,
            ILoadingScreen loadingScreen,
            IGlobalCamera globalCamera,
            IMetaState metaState,
            IClientTelemetry telemetry)
        {
            _menuLoader = menuLoader;
            _gamePlayLoader = gamePlayLoader;
            _loadingScreen = loadingScreen;
            _globalCamera = globalCamera;
            _metaState = metaState;
            _telemetry = telemetry;
        }

        private readonly IMenuLoader _menuLoader;
        private readonly IGamePlayLoader _gamePlayLoader;
        private readonly ILoadingScreen _loadingScreen;
        private readonly IGlobalCamera _globalCamera;
        private readonly IMetaState _metaState;
        private readonly IClientTelemetry _telemetry;

        // The telemetry writes every part of the loop into its own session, opened before the scope of the part is loaded:
        // a load that fails or kills the page belongs to the part. A rematch is a new match: a new session.
        private const string MenuTelemetryTag = "menu";
        private const string MatchTelemetryTag = "match";

        public void Start(IReadOnlyLifetime lifetime)
        {
            Loop(lifetime).Forget();
        }

        private UniTask Loop(IReadOnlyLifetime lifetime)
        {
            Menu().Forget();

            return UniTask.CompletedTask;

            async UniTask Menu()
            {
                StartMenuSession(lifetime).Forget();

                var menuResult = await _menuLoader.Load();

                Game(menuResult).Forget();
            }

            async UniTask Game(GameLoadData loadData)
            {
                _telemetry.StartSession(lifetime, MatchTelemetryTag);

                var transition = await _gamePlayLoader.Load(loadData);

                switch (transition)
                {
                    case GameEndTransition.Exit exit:
                    {
                        _globalCamera.Enable();
                        await _loadingScreen.Show();
                        Menu().Forget();
                        break;
                    }
                    case GameEndTransition.Rematch rematch:
                    {
                        Game(new GameLoadData()
                                    {
                                        Result = new SharedMatchmaking.MatchResult()
                                        {
                                            ServerUrl = rematch.NewSession.ServerUrl,
                                            SessionId = rematch.NewSession.SessionId,
                                            Ticket = rematch.NewSession.Ticket,
                                            Type = loadData.Result.Type
                                        }
                                    }
                                )
                            .Forget();

                        break;
                    }
                }
            }
        }

        // The first menu loads while the meta socket connects, and a session goes over that socket only after its
        // authorization (ITelemetrySocket): the first session opens on it, every later one at once. The menu waits for the
        // ready meta itself, so its result never comes before this session.
        private async UniTaskVoid StartMenuSession(IReadOnlyLifetime lifetime)
        {
            await _metaState.IsAuthorized.WaitTrue(lifetime);
            _telemetry.StartSession(lifetime, MenuTelemetryTag);
        }
    }
}