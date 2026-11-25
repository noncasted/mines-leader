using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

public static class AgentPlayerViews
{
    public static AgentPlayerView CreateEmpty()
    {
        return new AgentPlayerView
        {
            Modifiers = new List<string>(),
            Hand = new List<AgentCardView>(),
            Cells = new List<AgentCellView>(),
            BoardAscii = string.Empty
        };
    }

    public static AgentPlayerView Create(
        IPlayer player,
        bool hideHand,
        bool oracle,
        CardConfigOptions? cardConfigs)
    {
        var view = new AgentPlayerView
        {
            Id = player.User.Id,
            Health = player.Health?.Current ?? 0,
            HealthMax = player.Health?.ResultMax ?? 0,
            Mana = player.Mana?.Current ?? 0,
            ManaMax = player.Mana?.ResultMax ?? 0,
            MovesLeft = player.Moves?.Left ?? 0,
            MovesMax = player.Moves?.ResultMax ?? 0,
            MovesAvailable = player.Moves?.IsAvailable ?? false,
            DeckCount = player.Deck?.Count ?? 0,
            StashCount = player.Stash?.Count ?? 0,
            Modifiers = CollectModifiers(player.Modifiers),
            Hand = CollectHand(player, hideHand, cardConfigs)
        };

        AgentBoardViews.Fill(view, player.Board, oracle);
        return view;
    }

    private static List<string> CollectModifiers(IModifiers? modifiers)
    {
        var result = new List<string>();

        if (modifiers == null)
            return result;

        if (modifiers.Sources != null)
        {
            foreach (var source in modifiers.Sources)
            {
                if (source.Value != 0)
                    result.Add(source.Type.ToString());
            }
        }

        if (result.Count == 0 && modifiers.Values != null)
        {
            foreach (var (type, value) in modifiers.Values)
            {
                if (value != 0)
                    result.Add(type.ToString());
            }
        }

        return result;
    }

    private static List<AgentCardView> CollectHand(IPlayer player, bool hideHand, CardConfigOptions? cardConfigs)
    {
        var result = new List<AgentCardView>();
        var hand = player.Hand;

        if (hand?.Entries == null)
            return result;

        var configs = cardConfigs?.All;

        foreach (var card in hand.Entries)
        {
            if (hideHand)
            {
                result.Add(new AgentCardView
                {
                    Id = card.Id,
                    Type = "?",
                    ManaCost = 0
                });
                continue;
            }

            ICardConfig? config = null;
            configs?.TryGetValue(card.Type, out config);

            var info = AgentCardCatalog.Get(card.Type);

            result.Add(new AgentCardView
            {
                Id = card.Id,
                Type = card.Type.ToString(),
                ManaCost = config == null ? 0 : CardManaCost.Resolve(player, config),
                Target = (config?.Target ?? CardTarget.Self).ToString(),
                Shape = info.Shape,
                Size = AgentCardCatalog.ResolveSize(card.Type, config),
                NeedsPosition = CardUsePayloadFactory.CreateDefault(card.Type) is IBoardCardUsePayload,
                Summary = info.Summary
            });
        }

        return result;
    }
}
