using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Internal;
using Meta;
using Shared;
using UnityEngine;
#if UNITY_EDITOR
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
#endif

namespace Flow.Mocks
{
    public class GameMock : MockBase
    {
        /// <summary>
        /// The agent puts the JSON of <see cref="AgentMatchFixture"/> here before entering Play
        /// (tools/scripts/game-agent.py start --hand ...). The key is read once and removed,
        /// so the next Play without a fixture goes as usual. The scene is not changed.
        /// </summary>
        public const string FixturePrefsKey = "MinesLeader.GameMock.Fixture";

#if UNITY_EDITOR
        private static readonly ILog _log = Logs.Of<GameMock>();
#endif

        [SerializeField] private GameMatchType _mode;

        public override async UniTask Process()
        {
            var scope = await Bootstrap();

            // There is no menu here, which usually waits for the meta: the game needs the card registries and the connection.
            await scope.Resolve<IMetaState>().IsReady.WaitTrue(scope.Lifetime);

            var scopeLoaderFactory = scope.Resolve<IServiceScopeLoader>();
            var matchmaking = scope.Resolve<IMatchmaking>();

            var fixture = ReadFixture();
            var sessionData = await matchmaking.CreateGameWithBot(scope.Lifetime, _mode, fixture);
            var gameScope = await scopeLoaderFactory.LoadPvPMock(scope, sessionData);

            GameProfiler.Finish();

            var loop = gameScope.Resolve<IGamePlayLoop>();
            await loop.Process(gameScope.Lifetime, sessionData);
        }

        private static AgentMatchFixture ReadFixture()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.HasKey(FixturePrefsKey) == false)
                return null;

            var json = UnityEditor.EditorPrefs.GetString(FixturePrefsKey);
            UnityEditor.EditorPrefs.DeleteKey(FixturePrefsKey);

            if (string.IsNullOrEmpty(json))
                return null;

            _log.Info("Applying agent fixture: {Json}", json);
            return JsonConvert.DeserializeObject<AgentMatchFixture>(json, new StringEnumConverter());
#else
            return null;
#endif
        }
    }
}