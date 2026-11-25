using System.Threading.Tasks;
using System;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace GamePlay.Editor.Agent
{
    [McpForUnityTool("game_use_card", Description = "Play a hand card by id or CardType name.")]
    public static class GameUseCardTool
    {
        public sealed class Parameters
        {
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

        public static Task<object> HandleCommand(JObject parameters)
        {
            Guid? cardId = null;
            var cardIdText = GameAgentMcpTools.GetString(parameters, "card_id");

            if (string.IsNullOrWhiteSpace(cardIdText) == false)
            {
                if (Guid.TryParse(cardIdText, out var parsedCard) == false)
                    return Task.FromResult<object>(GameAgentMcpTools.Error("Invalid card_id"));

                cardId = parsedCard;
            }

            Guid? extraCardId = null;
            var extraText = GameAgentMcpTools.GetString(parameters, "extra_card_id");

            if (string.IsNullOrWhiteSpace(extraText) == false)
            {
                if (Guid.TryParse(extraText, out var parsedExtra) == false)
                    return Task.FromResult<object>(GameAgentMcpTools.Error("Invalid extra_card_id"));

                extraCardId = parsedExtra;
            }

            var type = GameAgentMcpTools.GetString(parameters, "type");
            var x = GameAgentMcpTools.GetNullableInt(parameters, "x");
            var y = GameAgentMcpTools.GetNullableInt(parameters, "y");
            var chosenIndex = GameAgentMcpTools.GetNullableInt(parameters, "chosen_index");

            return GameAgentMcpTools.Run(agent => agent.UseCard(cardId, type, x, y, extraCardId, chosenIndex));
        }
    }
}
