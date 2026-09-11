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
        /// Агент кладёт сюда JSON <see cref="AgentMatchFixture"/> перед входом в Play
        /// (tools/scripts/game-agent.py start --hand ...). Ключ читается один раз и удаляется,
        /// поэтому следующий Play без фикстуры идёт как обычно. Сцена не меняется.
        /// </summary>
        public const string FixturePrefsKey = "MinesLeader.GameMock.Fixture";

        [SerializeField] private GameMatchType _mode;

        public override async UniTaskVoid Process()
        {
            var scope = await Bootstrap();

            // Меню, которое обычно ждёт мету, здесь нет: игре нужны реестры карт и подключение.
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

            Debug.Log($"[GameMock] Applying agent fixture: {json}");
            return JsonConvert.DeserializeObject<AgentMatchFixture>(json, new StringEnumConverter());
#else
            return null;
#endif
        }
    }
}