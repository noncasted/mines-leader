using Cluster.Configs;
using Shared;

namespace Game.GamePlay;

public interface ICardFactory
{
    ICard Create(IPlayer owner, MoveSnapshot snapshot, ICardUsePayload payload);
}

public class CardFactory : ICardFactory
{
    public CardFactory(
        IGameContext gameContext,
        IRoundActionService roundActionService,
        ICardConfigs configs,
        IGameRandom gameRandom)
    {
        _gameContext = gameContext;
        _roundActionService = roundActionService;
        _configs = configs;
        _gameRandom = gameRandom;
    }

    private readonly IGameContext _gameContext;
    private readonly IRoundActionService _roundActionService;
    private readonly ICardConfigs _configs;
    private readonly IGameRandom _gameRandom;

    public ICard Create(IPlayer owner, MoveSnapshot snapshot, ICardUsePayload payload)
    {
        return payload.Type switch
        {
            CardType.Trebuchet => new Trebuchet(owner,
                GetBoard(_gameContext.GetOpponent(owner), payload),
                _configs.Value.Trebuchet_Normal,
                (CardUsePayload.Trebuchet)payload),
            CardType.Trebuchet_Max => new Trebuchet(owner,
                GetBoard(_gameContext.GetOpponent(owner), payload),
                _configs.Value.Trebuchet_Normal,
                (CardUsePayload.Trebuchet)payload),
            CardType.Bloodhound => new Bloodhound(GetBoard(owner, payload),
                _configs.Value.BloodHound_Normal,
                (CardUsePayload.Bloodhound)payload),
            CardType.Bloodhound_Max => new Bloodhound(GetBoard(owner, payload),
                _configs.Value.BloodHound_Max,
                (CardUsePayload.Bloodhound)payload),
            CardType.ZipZap => new ZipZap(owner,
                GetBoard(owner, payload),
                snapshot,
                _configs.Value.ZipZap_Normal,
                (CardUsePayload.ZipZap)payload),
            CardType.ZipZap_Max => new ZipZap(owner,
                GetBoard(owner, payload),
                snapshot,
                _configs.Value.ZipZap_Max,
                (CardUsePayload.ZipZap)payload),
            CardType.TrebuchetAimer => new TrebuchetAimer(owner,
                _configs.Value.TrebuchetAimer_Normal),
            CardType.TrebuchetAimer_Max => new TrebuchetAimer(owner,
                _configs.Value.TrebuchetAimer_Max),
            CardType.ErosionDozer => new ErosionDozer(GetBoard(owner, payload),
                _configs.Value.ErosionDozer_Normal,
                (CardUsePayload.ErosionDozer)payload),
            CardType.ErosionDozer_Max => new ErosionDozer(GetBoard(owner, payload),
                _configs.Value.ErosionDozer_Max,
                (CardUsePayload.ErosionDozer)payload),
            CardType.Gravedigger => new GraveDigger(owner, snapshot),
            CardType.OpponentBomb => new OpponentBomb(_gameContext.GetOpponent(owner),
                GetBoard(_gameContext.GetOpponent(owner), payload),
                (CardUsePayload.OpponentBomb)payload),
            CardType.OpponentFlagErase => new OpponentFlagErase(GetBoard(_gameContext.GetOpponent(owner), payload),
                _configs.Value.OpponentFlagErase_Normal,
                (CardUsePayload.OpponentFlagErase)payload),
            CardType.OpponentFlagErase_Max => new OpponentFlagErase(GetBoard(_gameContext.GetOpponent(owner), payload),
                _configs.Value.OpponentFlagErase_Max,
                (CardUsePayload.OpponentFlagErase)payload),
            CardType.OpponentFlagReshuffle => new OpponentFlagReshuffle(
                GetBoard(_gameContext.GetOpponent(owner), payload),
                _configs.Value.OpponentFlagReshuffle_Normal,
                (CardUsePayload.OpponentFlagReshuffle)payload,
                owner, _gameRandom),
            CardType.OpponentFlagReshuffle_Max => new OpponentFlagReshuffle(
                GetBoard(_gameContext.GetOpponent(owner), payload),
                _configs.Value.OpponentFlagReshuffle_Max,
                (CardUsePayload.OpponentFlagReshuffle)payload,
                owner, _gameRandom),
            CardType.Smoke => new Smoke(GetBoard(_gameContext.GetOpponent(owner), payload),
                (CardUsePayload.Smoke)payload,
                _configs.Value.Smoke_Normal,
                _roundActionService),
            CardType.Smoke_Max => new Smoke(GetBoard(_gameContext.GetOpponent(owner), payload),
                (CardUsePayload.Smoke)payload,
                _configs.Value.Smoke_Max,
                _roundActionService),
            CardType.Medic => new Medic(owner),
            CardType.MinefieldScout => new MinefieldScout(GetBoard(owner, payload),
                (CardUsePayload.MinefieldScout)payload,
                _configs.Value.MinefieldScout_Normal),
            CardType.MinefieldScout_Max => new MinefieldScout(GetBoard(owner, payload),
                (CardUsePayload.MinefieldScout)payload,
                _configs.Value.MinefieldScout_Max),
            CardType.Siphon => new Siphon(owner, _gameContext.GetOpponent(owner), _configs.Value.Siphon_Normal),
            CardType.ChainReaction => new ChainReaction(GetBoard(_gameContext.GetOpponent(owner), payload),
                (CardUsePayload.ChainReaction)payload,
                _configs.Value.ChainReaction_Normal),
            CardType.Overclock => new Overclock(owner, _configs.Value.Overclock_Normal),
            CardType.FogOfWar => new FogOfWar(GetBoard(_gameContext.GetOpponent(owner), payload),
                (CardUsePayload.FogOfWar)payload,
                _configs.Value.FogOfWar_Normal,
                _roundActionService),
            CardType.FogOfWar_Max => new FogOfWar(GetBoard(_gameContext.GetOpponent(owner), payload),
                (CardUsePayload.FogOfWar)payload,
                _configs.Value.FogOfWar_Max,
                _roundActionService),
            CardType.Scavenger => new Scavenger(owner, snapshot, _configs.Value.Scavenger_Normal),
            CardType.HandScramble => new HandScramble(_gameContext.GetOpponent(owner), snapshot),
            CardType.Lockdown => new Lockdown(_gameContext.GetOpponent(owner),
                _configs.Value.Lockdown_Normal,
                _roundActionService),
            CardType.Sonar => new Sonar(GetBoard(owner, payload),
                _configs.Value.Sonar_Normal,
                (CardUsePayload.Sonar)payload),
            CardType.Purge => new Purge(owner),
            CardType.Adrenaline => new Adrenaline(owner, _configs.Value.Adrenaline_Normal),
            CardType.ManaSurge => new ManaSurge(owner, _configs.Value.ManaSurge_Normal, _roundActionService),
            CardType.BloodPact => new BloodPact(owner, _configs.Value.BloodPact_Normal, _roundActionService),
            CardType.CoinToss => new CoinToss(owner, _configs.Value.CoinToss_Normal, _gameRandom),
            CardType.ManaFountain => new ManaFountain(owner, _configs.Value.ManaFountain_Normal, _roundActionService, _gameRandom),
            CardType.Focus => new Focus(owner, _configs.Value.Focus_Normal),
            CardType.Shield => new Shield(owner),
            CardType.PowerSurge => new PowerSurge(owner, _configs.Value.PowerSurge_Normal, _roundActionService),
            CardType.Embargo => new Embargo(_gameContext.GetOpponent(owner), _configs.Value.Embargo_Normal, _roundActionService),
            CardType.Recycler => new Recycler(owner, snapshot, _configs.Value.Recycler_Normal, (CardUsePayload.Recycler)payload),
            CardType.MysticDraw => new MysticDraw(owner, snapshot, _configs.Value.MysticDraw_Normal, _gameRandom),
            CardType.DoubleOrNothing => new DoubleOrNothing(owner, _gameRandom),
            CardType.GamblersRuin => new GamblersRuin(owner, snapshot, _configs.Value.GamblersRuin_Normal, _roundActionService, _gameRandom),
            CardType.Excavator => new Excavator(GetBoard(owner, payload), _configs.Value.Excavator_Normal, (CardUsePayload.Excavator)payload),
            CardType.Excavator_Max => new Excavator(GetBoard(owner, payload), _configs.Value.Excavator_Max, (CardUsePayload.Excavator)payload),
            CardType.ThermalVision => new ThermalVision(GetBoard(owner, payload), _configs.Value.ThermalVision_Normal, (CardUsePayload.ThermalVision)payload),
            CardType.ThermalVision_Max => new ThermalVision(GetBoard(owner, payload), _configs.Value.ThermalVision_Max, (CardUsePayload.ThermalVision)payload),
            CardType.ChaosDiamond => new ChaosDiamond(GetBoard(owner, payload), _configs.Value.ChaosDiamond_Normal, (CardUsePayload.ChaosDiamond)payload, owner, _gameRandom),
            CardType.ChaosScout => new ChaosScout(GetBoard(owner, payload), _configs.Value.ChaosScout_Normal, (CardUsePayload.ChaosScout)payload, owner, _gameRandom),
            CardType.MineCluster => new MineCluster(owner, GetBoard(_gameContext.GetOpponent(owner), payload), _configs.Value.MineCluster_Normal, (CardUsePayload.MineCluster)payload),
            CardType.MineCluster_Max => new MineCluster(owner, GetBoard(_gameContext.GetOpponent(owner), payload), _configs.Value.MineCluster_Max, (CardUsePayload.MineCluster)payload),
            CardType.CarpetBomb => new CarpetBomb(owner, GetBoard(_gameContext.GetOpponent(owner), payload), _configs.Value.CarpetBomb_Normal, (CardUsePayload.CarpetBomb)payload),
            CardType.CarpetBomb_Max => new CarpetBomb(owner, GetBoard(_gameContext.GetOpponent(owner), payload), _configs.Value.CarpetBomb_Max, (CardUsePayload.CarpetBomb)payload),
            CardType.FortuneBlast => new FortuneBlast(owner, GetBoard(_gameContext.GetOpponent(owner), payload), _configs.Value.FortuneBlast_Normal, (CardUsePayload.FortuneBlast)payload, _gameRandom),
            CardType.ChaosFog => new ChaosFog(GetBoard(_gameContext.GetOpponent(owner), payload), (CardUsePayload.ChaosFog)payload, _configs.Value.ChaosFog_Normal, _roundActionService, owner, _gameRandom),
            CardType.Frost => new Frost(GetBoard(_gameContext.GetOpponent(owner), payload), _configs.Value.Frost_Normal, (CardUsePayload.Frost)payload, _roundActionService),
            CardType.Frost_Max => new Frost(GetBoard(_gameContext.GetOpponent(owner), payload), _configs.Value.Frost_Max, (CardUsePayload.Frost)payload, _roundActionService),
            CardType.Blackout => new Blackout(GetBoard(_gameContext.GetOpponent(owner), payload), _configs.Value.Blackout_Normal, (CardUsePayload.Blackout)payload, _roundActionService),
            CardType.Blackout_Max => new Blackout(GetBoard(_gameContext.GetOpponent(owner), payload), _configs.Value.Blackout_Max, (CardUsePayload.Blackout)payload, _roundActionService),
            CardType.FortuneCookie => new FortuneCookie(owner, owner.Board, _configs.Value.FortuneCookie_Normal, _gameRandom),
            CardType.Salvage => new Salvage(owner, snapshot, _configs.Value.Salvage_Normal, (CardUsePayload.Salvage)payload),
            CardType.CardThief => new CardThief(owner, _gameContext.GetOpponent(owner), snapshot, _gameRandom),
            CardType.SabotageDeck => new SabotageDeck(_gameContext.GetOpponent(owner)),
            CardType.Dud => new Dud(owner),
            CardType.SoulLink => new SoulLink(owner, _gameContext.GetOpponent(owner), _configs.Value.SoulLink_Normal, _roundActionService),
            CardType.MirrorMatch => new MirrorMatch(owner, _gameContext.GetOpponent(owner), this, snapshot),
            CardType.DimensionRift => new DimensionRift(owner, owner.Board, GetBoard(_gameContext.GetOpponent(owner), payload), _configs.Value.DimensionRift_Normal, (CardUsePayload.DimensionRift)payload),
            _ => throw new ArgumentOutOfRangeException(nameof(payload.Type), payload.Type, null)
        };
    }

    IBoard GetBoard(IPlayer owner, ICardUsePayload payload)
    {
        if (payload is not IBoardCardUsePayload boardPayload)
            throw new ArgumentException("Value must implement IBoardCardUsePayload", nameof(payload));

        var board = owner.Board;
        board.EnsureGenerated(boardPayload.Position);

        return board;
    }
}