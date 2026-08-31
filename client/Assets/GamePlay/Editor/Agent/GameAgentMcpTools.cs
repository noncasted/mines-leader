using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Flow.Mocks;
using GamePlay.Agent;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using Shared;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Unity's MCP window lists these tools with x/y, but Coplay HTTP does not
// publish parameterized custom tools to tools/list. Agents must call them via
// execute_custom_tool(tool_name="game_open", parameters={x, y})
// or the CLI: tools/scripts/game-agent.py open X Y

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

            if (HasGameMock() == false)
                return Error("Game agent only runs in GameMock play mode on the game field");

            try {
                using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(30));
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

        private static JToken Token(JObject parameters, string name) {
            if (parameters == null)
                return null;

            if (parameters.TryGetValue(name, StringComparison.OrdinalIgnoreCase, out var token))
                return token;

            return null;
        }

        private static bool HasGameMock() {
            return Object.FindFirstObjectByType<GameMock>(FindObjectsInactive.Include) != null;
        }
    }

    [McpForUnityTool("game_start_vs_bot", Description = "Wait until a GameMock LastManStandingTurnBased match is active, or return current status if one is already running.")]
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
        public sealed class Parameters {
            [ToolParameter("Rebuild from the server with mines visible. Ignored unless the match IncludeOracle flag is on.", Required = false, DefaultValue = "false")]
            public bool oracle { get; set; }
        }

        public static Task<object> HandleCommand(JObject parameters) {
            if (GameAgentMcpTools.GetBool(parameters, "oracle", false))
                return GameAgentMcpTools.Run(GameAgentBridge.RequestOracleState());

            return Task.FromResult<object>(GameAgentBridge.GetState(false));
        }
    }

    [McpForUnityTool("game_wait_turn", Description = "Wait until it is the human turn or the match is over.")]
    public static class GameWaitTurnTool {
        public sealed class Parameters {
            [ToolParameter("Milliseconds to wait.", Required = false, DefaultValue = "60000")]
            public int timeout_ms { get; set; }
        }

        public static Task<object> HandleCommand(JObject parameters) {
            var timeoutMs = GameAgentMcpTools.GetInt(parameters, "timeout_ms", 60000);
            return GameAgentMcpTools.Run(GameAgentBridge.WaitOwnTurn(timeoutMs));
        }
    }

    [McpForUnityTool("game_open", Description = "Open a cell on the player's board.")]
    public static class GameOpenTool {
        public sealed class Parameters {
            [ToolParameter("Board X coordinate.")]
            public int x { get; set; }

            [ToolParameter("Board Y coordinate.")]
            public int y { get; set; }
        }

        public static Task<object> HandleCommand(JObject parameters) {
            var x = GameAgentMcpTools.GetInt(parameters, "x", 0);
            var y = GameAgentMcpTools.GetInt(parameters, "y", 0);
            return GameAgentMcpTools.Run(GameAgentBridge.Open(x, y));
        }
    }

    [McpForUnityTool("game_chord", Description = "Chord-open a cell on the player's board.")]
    public static class GameChordTool {
        public sealed class Parameters {
            [ToolParameter("Board X coordinate.")]
            public int x { get; set; }

            [ToolParameter("Board Y coordinate.")]
            public int y { get; set; }
        }

        public static Task<object> HandleCommand(JObject parameters) {
            var x = GameAgentMcpTools.GetInt(parameters, "x", 0);
            var y = GameAgentMcpTools.GetInt(parameters, "y", 0);
            return GameAgentMcpTools.Run(GameAgentBridge.Chord(x, y));
        }
    }

    [McpForUnityTool("game_flag", Description = "Flag a cell. Legal during the opponent turn.")]
    public static class GameFlagTool {
        public sealed class Parameters {
            [ToolParameter("Board X coordinate.")]
            public int x { get; set; }

            [ToolParameter("Board Y coordinate.")]
            public int y { get; set; }
        }

        public static Task<object> HandleCommand(JObject parameters) {
            var x = GameAgentMcpTools.GetInt(parameters, "x", 0);
            var y = GameAgentMcpTools.GetInt(parameters, "y", 0);
            return GameAgentMcpTools.Run(GameAgentBridge.Flag(x, y));
        }
    }

    [McpForUnityTool("game_unflag", Description = "Remove a flag. Legal during the opponent turn.")]
    public static class GameUnflagTool {
        public sealed class Parameters {
            [ToolParameter("Board X coordinate.")]
            public int x { get; set; }

            [ToolParameter("Board Y coordinate.")]
            public int y { get; set; }
        }

        public static Task<object> HandleCommand(JObject parameters) {
            var x = GameAgentMcpTools.GetInt(parameters, "x", 0);
            var y = GameAgentMcpTools.GetInt(parameters, "y", 0);
            return GameAgentMcpTools.Run(GameAgentBridge.Unflag(x, y));
        }
    }

    [McpForUnityTool("game_use_card", Description = "Play a hand card by id or CardType name.")]
    public static class GameUseCardTool {
        public sealed class Parameters {
            [ToolParameter("Hand card id. Wins over type when both are set.", Required = false)]
            public string card_id { get; set; }

            [ToolParameter("CardType name, e.g. Bloodhound. First matching hand card is used.", Required = false)]
            public string type { get; set; }

            [ToolParameter("Target X, if the card needs a cell.", Required = false)]
            public int? x { get; set; }

            [ToolParameter("Target Y, if the card needs a cell.", Required = false)]
            public int? y { get; set; }

            [ToolParameter("Second card id for ZipZap.", Required = false)]
            public string extra_card_id { get; set; }

            [ToolParameter("Choice index for cards that pick an option.", Required = false)]
            public int? chosen_index { get; set; }
        }

        public static Task<object> HandleCommand(JObject parameters) {
            Guid? cardId = null;
            var cardIdText = GameAgentMcpTools.GetString(parameters, "card_id");
            if (string.IsNullOrWhiteSpace(cardIdText) == false) {
                if (Guid.TryParse(cardIdText, out var parsedCard) == false)
                    return Task.FromResult<object>(GameAgentMcpTools.Error("Invalid card_id"));

                cardId = parsedCard;
            }

            Guid? extraCardId = null;
            var extraText = GameAgentMcpTools.GetString(parameters, "extra_card_id");
            if (string.IsNullOrWhiteSpace(extraText) == false) {
                if (Guid.TryParse(extraText, out var parsedExtra) == false)
                    return Task.FromResult<object>(GameAgentMcpTools.Error("Invalid extra_card_id"));

                extraCardId = parsedExtra;
            }

            return GameAgentMcpTools.Run(GameAgentBridge.UseCard(
                cardId,
                GameAgentMcpTools.GetString(parameters, "type"),
                GameAgentMcpTools.GetNullableInt(parameters, "x"),
                GameAgentMcpTools.GetNullableInt(parameters, "y"),
                extraCardId,
                GameAgentMcpTools.GetNullableInt(parameters, "chosen_index")));
        }
    }

    [McpForUnityTool("game_legal_plays", Description = "Ask the server which hand cards can be played and which cells they can target.")]
    public static class GameLegalPlaysTool {
        public static async Task<object> HandleCommand(JObject _) {
            try {
                return await GameAgentBridge.LegalPlays();
            }
            catch (Exception exception) {
                return GameAgentMcpTools.Error(exception.Message);
            }
        }
    }

    [McpForUnityTool("game_inspect_cell", Description = "Read the live Unity cell view (state, flag, minesAround, animator). Does not hit the network.")]
    public static class GameInspectCellTool {
        public sealed class Parameters {
            [ToolParameter("Board X coordinate.")]
            public int x { get; set; }

            [ToolParameter("Board Y coordinate.")]
            public int y { get; set; }

            [ToolParameter("Inspect the opponent board instead of self.", Required = false, DefaultValue = "false")]
            public bool opponent { get; set; }
        }

        public static object HandleCommand(JObject parameters) {
            return GameAgentBridge.InspectCell(
                GameAgentMcpTools.GetInt(parameters, "x", 0),
                GameAgentMcpTools.GetInt(parameters, "y", 0),
                GameAgentMcpTools.GetBool(parameters, "opponent", false));
        }
    }

    [McpForUnityTool("game_wait_visual", Description = "Wait until no own-board cell animation is playing.")]
    public static class GameWaitVisualTool {
        public sealed class Parameters {
            [ToolParameter("Milliseconds to wait.", Required = false, DefaultValue = "5000")]
            public int timeout_ms { get; set; }
        }

        public static Task<object> HandleCommand(JObject parameters) {
            var timeoutMs = GameAgentMcpTools.GetInt(parameters, "timeout_ms", 5000);
            return GameAgentMcpTools.Run(GameAgentBridge.WaitVisual(timeoutMs));
        }
    }

    [McpForUnityTool("game_end_turn", Description = "Skip the remaining turn and wait for the next own turn or game over.")]
    public static class GameEndTurnTool {
        public sealed class Parameters {
            [ToolParameter("Milliseconds to wait for the next own turn.", Required = false, DefaultValue = "60000")]
            public int timeout_ms { get; set; }
        }

        public static Task<object> HandleCommand(JObject parameters) {
            var timeoutMs = GameAgentMcpTools.GetInt(parameters, "timeout_ms", 60000);
            return GameAgentMcpTools.Run(GameAgentBridge.EndTurn(timeoutMs));
        }
    }
}
