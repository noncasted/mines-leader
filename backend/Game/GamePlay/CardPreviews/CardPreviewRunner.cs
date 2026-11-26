using Cluster.Configs;
using Infrastructure;
using Internal;
using Game.GamePlay.Boards;
using Game.Session;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.GamePlay.CardPreviews;

// Runs one card of a preview scenario with the real card configs.
internal sealed class CardPreviewRunner
{
    public CardPreviewRunner(ICardConfigs configs)
    {
        _configs = configs;
    }

    private readonly ICardConfigs _configs;

    public CardUseResult RunCard(CardType cardType, Position target, PreviewPlayer invoker, CardUseContext context)
    {
        switch (cardType)
        {
            // --- Scout (own board) ---

            case CardType.ErosionDozer:
            case CardType.ErosionDozer_Max:
                return new ErosionDozer(_configs).Use(context, new CardUsePayload.ErosionDozer
                {
                    Type = cardType,
                    Position = target
                });

            case CardType.Sonar:
                return new Sonar(_configs).Use(context, new CardUsePayload.Sonar
                {
                    Type = cardType,
                    Position = target
                });

            case CardType.Excavator:
            case CardType.Excavator_Max:
                return new Excavator(_configs).Use(context, new CardUsePayload.Excavator
                {
                    Type = cardType,
                    Position = target
                });

            case CardType.MinefieldScout:
            case CardType.MinefieldScout_Max:
                return new MinefieldScout(_configs).Use(context, new CardUsePayload.MinefieldScout
                {
                    Type = cardType,
                    Position = target
                });

            case CardType.Bloodhound:
            case CardType.Bloodhound_Max:
                return new Bloodhound(_configs).Use(context, new CardUsePayload.Bloodhound
                {
                    Type = cardType,
                    Position = target
                });

            case CardType.ZipZap:
            case CardType.ZipZap_Max:
                return new ZipZap(_configs).Use(context, new CardUsePayload.ZipZap
                {
                    Type = cardType,
                    Position = target
                });

            // --- Effect scout (uses round-action service to schedule dispose) ---

            case CardType.ThermalVision:
            case CardType.ThermalVision_Max:
                return new ThermalVision(_configs, new PreviewRoundActionService())
                    .Use(context, new CardUsePayload.ThermalVision
                    {
                        Type = cardType,
                        Position = target
                    });

            // --- CrossBoard (opponent = the parsed board; self gets a tiny throwaway board) ---

            case CardType.MineCluster:
            case CardType.MineCluster_Max:
                return RunCrossBoard(target, context, (ctx, payload) => new MineCluster(_configs, ctx).Use(
                    payload.Context,
                    new CardUsePayload.MineCluster { Type = cardType, Position = target }));

            case CardType.CarpetBomb:
            case CardType.CarpetBomb_Max:
                return RunCrossBoard(target, context, (ctx, payload) => new CarpetBomb(_configs, ctx).Use(
                    payload.Context,
                    new CardUsePayload.CarpetBomb { Type = cardType, Position = target }));

            case CardType.Trebuchet:
            case CardType.Trebuchet_Max:
                return RunCrossBoard(target, context, (ctx, payload) => new Trebuchet(_configs, ctx).Use(
                    payload.Context,
                    new CardUsePayload.Trebuchet { Type = cardType, Position = target }));

            case CardType.OpponentBomb:
                return RunCrossBoard(target, context, (ctx, _) => new OpponentBomb(ctx).Use(_.Context,
                    new CardUsePayload.OpponentBomb { Type = cardType, Position = target }));

            case CardType.OpponentFlagErase:
            case CardType.OpponentFlagErase_Max:
                return RunCrossBoard(target, context, (ctx, payload) => new OpponentFlagErase(_configs, ctx).Use(
                    payload.Context,
                    new CardUsePayload.OpponentFlagErase { Type = cardType, Position = target }));

            case CardType.ChainReaction:
                return RunCrossBoard(target, context, (ctx, payload) => new ChainReaction(_configs, ctx).Use(
                    payload.Context,
                    new CardUsePayload.ChainReaction { Type = cardType, Position = target }));

            case CardType.Smoke:
            case CardType.Smoke_Max:
                return RunCrossBoard(target, context, (ctx, payload) =>
                    new Smoke(_configs, new PreviewRoundActionService(), ctx).Use(payload.Context,
                        new CardUsePayload.Smoke { Type = cardType, Position = target }));

            case CardType.Blackout:
            case CardType.Blackout_Max:
                return RunCrossBoard(target, context, (ctx, payload) =>
                    new Blackout(_configs, new PreviewRoundActionService(), ctx).Use(payload.Context,
                        new CardUsePayload.Blackout { Type = cardType, Position = target }));

            case CardType.Frost:
            case CardType.Frost_Max:
                return RunCrossBoard(target, context, (ctx, payload) =>
                    new Frost(_configs, new PreviewRoundActionService(), ctx).Use(payload.Context,
                        new CardUsePayload.Frost { Type = cardType, Position = target }));

            case CardType.FogOfWar:
            case CardType.FogOfWar_Max:
                return RunCrossBoard(target, context, (ctx, payload) =>
                    new FogOfWar(_configs, new PreviewRoundActionService(), ctx).Use(payload.Context,
                        new CardUsePayload.FogOfWar { Type = cardType, Position = target }));

            // --- Random-sized field cards (PreviewGameRandom returns max bound) ---

            case CardType.ChaosDiamond:
                return new ChaosDiamond(_configs, new PreviewGameRandom())
                    .Use(context, new CardUsePayload.ChaosDiamond { Type = cardType, Position = target });

            case CardType.ChaosScout:
                return new ChaosScout(_configs, new PreviewGameRandom())
                    .Use(context, new CardUsePayload.ChaosScout { Type = cardType, Position = target });

            case CardType.FortuneBlast:
                return RunCrossBoard(target, context, (ctx, payload) =>
                    new FortuneBlast(_configs, ctx, new PreviewGameRandom()).Use(payload.Context,
                        new CardUsePayload.FortuneBlast { Type = cardType, Position = target }));

            case CardType.ChaosFog:
                return RunCrossBoard(target, context, (ctx, payload) =>
                    new ChaosFog(_configs, new PreviewRoundActionService(), ctx, new PreviewGameRandom())
                        .Use(payload.Context,
                            new CardUsePayload.ChaosFog { Type = cardType, Position = target }));

            case CardType.OpponentFlagReshuffle:
            case CardType.OpponentFlagReshuffle_Max:
                return RunCrossBoard(target, context, (ctx, payload) =>
                    new OpponentFlagReshuffle(_configs, ctx, new PreviewGameRandom())
                        .Use(payload.Context,
                            new CardUsePayload.OpponentFlagReshuffle { Type = cardType, Position = target }));

            default:
                throw new NotSupportedException($"CardPreviewGenerator does not yet support {cardType}. " +
                                                "Add a handler in CardPreviewGenerator.RunCard.");
        }
    }


    /// <summary>
    /// Runs a cross-board card where the parsed scenario board is the opponent's board.
    /// Self gets a minimal throwaway board so IPlayer.Board isn't null.
    /// The <paramref name="run"/> callback receives a fresh <see cref="IGameContext"/>
    /// and a <see cref="CrossBoardInvoke"/> wrapper carrying the self-invoker <see cref="CardUseContext"/>.
    /// </summary>
    private CardUseResult RunCrossBoard(
        Position target,
        CardUseContext originalContext,
        Func<IGameContext, CrossBoardInvoke, CardUseResult> run)
    {
        var opponentPlayer = (PreviewPlayer)originalContext.Invoker;

        var (selfBoard, _) = BoardLayoutParser.Parse("""
                                                     t t
                                                     t t
                                                     """, Guid.Empty);
        var selfInvoker = new PreviewPlayer(selfBoard);

        var gameContext = new PreviewGameContext(selfInvoker, opponentPlayer);

        var selfUseContext = new CardUseContext
        {
            Invoker = selfInvoker,
            Snapshot = originalContext.Snapshot,
            CardId = originalContext.CardId
        };

        return run(gameContext, new CrossBoardInvoke(selfUseContext, target));
    }

    private readonly record struct CrossBoardInvoke(CardUseContext Context, Position Target);
}
