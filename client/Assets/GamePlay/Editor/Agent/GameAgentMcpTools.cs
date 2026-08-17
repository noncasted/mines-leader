using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Flow;
using GamePlay.Agent;
using GamePlay.Loop;
using Internal;
using MCPForUnity.Editor.Tools;
using Menu.Common;
using Menu.Main;
using Meta;
using Newtonsoft.Json.Linq;
using Shared;
using UnityEditor;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace GamePlay.Editor.Agent {
    public static class GameAgentMcpTools {
        public static async Task<object> Run(UniTask<SharedAgentObservation> task) {
            try {
                return await task;
            }
            catch (Exception exception) {
                return Error(exception.Message);
            }
        }

        public static SharedAgentObservation Error(string error) {
            return new SharedAgentObservation {
                HasError = true,
                Error = error ?? string.Empty
            };
        }

        public static object Status() {
            var observation = GameAgentBridge.LastObservation;
            return new {
                active = GameAgentBridge.IsActive,
                isOwnTurn = observation?.IsOwnTurn ?? false,
                gameOver = observation?.GameOver ?? false,
                sequence = observation?.Sequence ?? 0
            };
        }

        public static async Task<object> StartVsBot() {
            if (GameAgentBridge.IsActive)
                return Status();

            if (EditorApplication.isPlaying == false)
                return Error("Enter play mode with GameMock (mode LastManStandingTurnBased) and a running cluster");

            try {
                using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(30));

                if (HasGameMock() == false) {
                    var matchmaking = TryResolve<IMatchmaking>();
                    if (matchmaking != null) {
                        var lifetime = new Internal.Lifetime();
                        var sessionData = await matchmaking
                            .CreateGameWithBot(lifetime, GameMatchType.LastManStandingTurnBased)
                            .AttachExternalCancellation(cancel.Token);
                        await LoadCreatedMatch(sessionData);
                    }
                }

                await UniTask.WaitUntil(() => GameAgentBridge.IsActive, cancellationToken: cancel.Token);
            }
            catch (OperationCanceledException) {
                return Error("Timed out waiting for GameAgentBridge");
            }
            catch (Exception exception) {
                return Error(exception.Message);
            }

            if (GameAgentBridge.IsActive == false)
                return Error("Timed out waiting for GameAgentBridge");

            return Status();
        }

        public static int GetInt(JObject parameters, string name, int fallback) {
            var token = Token(parameters, name);
            if (token == null || token.Type == JTokenType.Null)
                return fallback;

            return token.Value<int>();
        }

        public static int? GetNullableInt(JObject parameters, string name) {
            var token = Token(parameters, name);
            if (token == null || token.Type == JTokenType.Null)
                return null;

            return token.Value<int>();
        }

        public static bool GetBool(JObject parameters, string name, bool fallback) {
            var token = Token(parameters, name);
            if (token == null || token.Type == JTokenType.Null)
                return fallback;

            return token.Value<bool>();
        }

        public static string GetString(JObject parameters, string name) {
            var token = Token(parameters, name);
            return token?.Type == JTokenType.Null ? null : token?.ToString();
        }

        private static async UniTask LoadCreatedMatch(SharedMatchmaking.MatchResult sessionData) {
            var play = TryResolve<IMenuPlay>();
            if (play?.MatchFound is EventSource<SharedMatchmaking.MatchResult> matchFound) {
                matchFound.Invoke(sessionData);
                return;
            }

            var gamePlayLoader = TryResolve<IGamePlayLoader>();
            if (gamePlayLoader != null) {
                gamePlayLoader.Load(new GameLoadData { Result = sessionData }).Forget();
                return;
            }

            var loopScopeLoader = TryResolve<IGameLoopScopeLoader>();
            if (loopScopeLoader == null)
                return;

            var gameScope = await loopScopeLoader.Load((loader, parent) => loader.LoadPvp(parent, sessionData));
            var loop = gameScope.Resolve<IPvPGameLoop>();
            loop.Process(gameScope.Lifetime, sessionData).Forget();
        }

        private static JToken Token(JObject parameters, string name) {
            if (parameters == null)
                return null;

            if (parameters.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var token))
                return token;

            return null;
        }

        private static bool HasGameMock() {
            var behaviours = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (var behaviour in behaviours) {
                if (behaviour.GetType().Name == "GameMock")
                    return true;
            }

            return false;
        }

        private static T TryResolve<T>() where T : class {
            var scopes = Object.FindObjectsByType<LifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (var scope in scopes) {
                var container = scope.Container;
                if (container == null)
                    continue;

                if (container.TryResolve<T>(out var resolved) && resolved != null)
                    return resolved;
            }

            return null;
        }
    }

    [McpForUnityTool("game_start_vs_bot", Description = "Start a LastManStandingTurnBased vs-bot match, or return current status if one is already running.")]
    public static class GameStartVsBotTool {
        public static Task<object> HandleCommand(JObject _) {
            return GameAgentMcpTools.StartVsBot();
        }
    }

    [McpForUnityTool("game_status", Description = "Return whether a match is active and whose turn it is.")]
    public static class GameStatusTool {
        public static object HandleCommand(JObject _) {
            return GameAgentMcpTools.Status();
        }
    }

    [McpForUnityTool("game_get_state", Description = "Return the last player-visible observation, or request an oracle rebuild when oracle is true.")]
    public static class GameGetStateTool {
        public static Task<object> HandleCommand(JObject parameters) {
            if (GameAgentMcpTools.GetBool(parameters, "oracle", false))
                return GameAgentMcpTools.Run(GameAgentBridge.RequestOracleState());

            return Task.FromResult<object>(GameAgentBridge.GetState(false));
        }
    }

    [McpForUnityTool("game_wait_turn", Description = "Wait until it is the human turn or the match is over.")]
    public static class GameWaitTurnTool {
        public static Task<object> HandleCommand(JObject parameters) {
            var timeoutMs = GameAgentMcpTools.GetInt(parameters, "timeout_ms", 60000);
            return GameAgentMcpTools.Run(GameAgentBridge.WaitOwnTurn(timeoutMs));
        }
    }

    [McpForUnityTool("game_open", Description = "Open a cell on the player's board.")]
    public static class GameOpenTool {
        public static Task<object> HandleCommand(JObject parameters) {
            var x = GameAgentMcpTools.GetInt(parameters, "x", 0);
            var y = GameAgentMcpTools.GetInt(parameters, "y", 0);
            return GameAgentMcpTools.Run(GameAgentBridge.Open(x, y));
        }
    }

    [McpForUnityTool("game_chord", Description = "Chord-open a cell on the player's board.")]
    public static class GameChordTool {
        public static Task<object> HandleCommand(JObject parameters) {
            var x = GameAgentMcpTools.GetInt(parameters, "x", 0);
            var y = GameAgentMcpTools.GetInt(parameters, "y", 0);
            return GameAgentMcpTools.Run(GameAgentBridge.Chord(x, y));
        }
    }

    [McpForUnityTool("game_flag", Description = "Flag a cell. Legal during the opponent turn.")]
    public static class GameFlagTool {
        public static Task<object> HandleCommand(JObject parameters) {
            var x = GameAgentMcpTools.GetInt(parameters, "x", 0);
            var y = GameAgentMcpTools.GetInt(parameters, "y", 0);
            return GameAgentMcpTools.Run(GameAgentBridge.Flag(x, y));
        }
    }

    [McpForUnityTool("game_unflag", Description = "Remove a flag. Legal during the opponent turn.")]
    public static class GameUnflagTool {
        public static Task<object> HandleCommand(JObject parameters) {
            var x = GameAgentMcpTools.GetInt(parameters, "x", 0);
            var y = GameAgentMcpTools.GetInt(parameters, "y", 0);
            return GameAgentMcpTools.Run(GameAgentBridge.Unflag(x, y));
        }
    }

    [McpForUnityTool("game_use_card", Description = "Play a hand card by id.")]
    public static class GameUseCardTool {
        public static Task<object> HandleCommand(JObject parameters) {
            var cardIdText = GameAgentMcpTools.GetString(parameters, "card_id");
            if (Guid.TryParse(cardIdText, out var cardId) == false)
                return Task.FromResult<object>(GameAgentMcpTools.Error("Invalid card_id"));

            Guid? extraCardId = null;
            var extraText = GameAgentMcpTools.GetString(parameters, "extra_card_id");
            if (string.IsNullOrWhiteSpace(extraText) == false) {
                if (Guid.TryParse(extraText, out var parsedExtra) == false)
                    return Task.FromResult<object>(GameAgentMcpTools.Error("Invalid extra_card_id"));

                extraCardId = parsedExtra;
            }

            return GameAgentMcpTools.Run(GameAgentBridge.UseCard(
                cardId,
                GameAgentMcpTools.GetNullableInt(parameters, "x"),
                GameAgentMcpTools.GetNullableInt(parameters, "y"),
                extraCardId,
                GameAgentMcpTools.GetNullableInt(parameters, "chosen_index")));
        }
    }

    [McpForUnityTool("game_end_turn", Description = "Skip the remaining turn and wait for the next own turn or game over.")]
    public static class GameEndTurnTool {
        public static Task<object> HandleCommand(JObject parameters) {
            var timeoutMs = GameAgentMcpTools.GetInt(parameters, "timeout_ms", 60000);
            return GameAgentMcpTools.Run(GameAgentBridge.EndTurn(timeoutMs));
        }
    }
}
