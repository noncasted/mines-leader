using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Flow.Mocks;
using GamePlay.Agent;
using Newtonsoft.Json.Linq;
using Shared;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// MCP for Unity calls a tool through its public static HandleCommand(JObject), found by reflection outside every
// container: the tools reach the match through GameAgentAccess. The server lists only the tools without parameters
// in tools/list; the others are called by name with execute_custom_tool (tools/scripts/game-agent.py does).

namespace GamePlay.Editor.Agent
{
    public static class GameAgentMcpTools
    {
        // A failure of a call (no match, a network error) is an answer of the tool, not an error of the server.
        public static async Task<object> Run<T>(Func<IGameAgent, UniTask<T>> call)
        {
            try
            {
                return await call(GameAgentAccess.Require());
            }
            catch (Exception exception)
            {
                return Error(exception.Message);
            }
        }

        public static Task<object> Read<T>(Func<IGameAgent, T> call)
        {
            try
            {
                return Task.FromResult<object>(call(GameAgentAccess.Require()));
            }
            catch (Exception exception)
            {
                return Task.FromResult<object>(Error(exception.Message));
            }
        }

        public static SharedAgentObservation Error(string error)
        {
            return new SharedAgentObservation
            {
                HasError = true,
                Error = error
            };
        }

        public static object Status()
        {
            var observation = GameAgentAccess.Current?.LastObservation;

            return new
            {
                active = GameAgentAccess.IsActive,
                isOwnTurn = observation?.IsOwnTurn ?? false,
                gameOver = observation?.GameOver ?? false,
                sequence = observation?.Sequence ?? 0
            };
        }

        public static async Task<object> StartVsBot()
        {
            if (GameAgentAccess.IsActive)
                return Status();

            if (EditorApplication.isPlaying == false)
                return Error("Enter play mode with GameMock (mode LastManStandingTurnBased) and a running cluster");

            if (Object.FindAnyObjectByType<GameMock>(FindObjectsInactive.Include) == null)
                return Error("Game agent only runs in GameMock play mode on the game field");

            try
            {
                using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await UniTask.WaitUntil(() => GameAgentAccess.IsActive, cancellationToken: cancel.Token);
            }
            catch (OperationCanceledException)
            {
                return Error("Timed out waiting for the agent match");
            }

            return Status();
        }

        public static int GetInt(JObject parameters, string name, int fallback)
        {
            var token = Token(parameters, name);

            if (token == null || token.Type == JTokenType.Null)
                return fallback;

            return token.Value<int>();
        }

        public static int? GetNullableInt(JObject parameters, string name)
        {
            var token = Token(parameters, name);

            if (token == null || token.Type == JTokenType.Null)
                return null;

            return token.Value<int>();
        }

        public static bool GetBool(JObject parameters, string name, bool fallback)
        {
            var token = Token(parameters, name);

            if (token == null || token.Type == JTokenType.Null)
                return fallback;

            return token.Value<bool>();
        }

        public static string GetString(JObject parameters, string name)
        {
            var token = Token(parameters, name);
            return token == null || token.Type == JTokenType.Null ? null : token.ToString();
        }

        // Optional parameters are absent from the object, or the whole object is absent for a call without any.
        private static JToken Token(JObject parameters, string name)
        {
            if (parameters == null)
                return null;

            return parameters.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var token) ? token : null;
        }
    }
}
