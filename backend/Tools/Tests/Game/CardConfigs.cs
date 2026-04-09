using Shared;
using Tests.Fixtures;

namespace Tests.Game;

/// <summary>
/// Cached card configs loaded from Orchestration/Coordinator/config.cards.json.
/// Same values as production.
/// </summary>
public static class CardConfigs
{
    private static readonly Lazy<CardConfigOptions> _options = new(() =>
        ConfigLoader.Load<CardConfigOptions>("config.cards"));

    public static CardConfigOptions All => _options.Value;

    public static CardConfigOptions.Bloodhound Bloodhound => All.BloodHound_Normal;
    public static CardConfigOptions.ErosionDozer ErosionDozer => All.ErosionDozer_Normal;
    public static CardConfigOptions.Trebuchet Trebuchet => All.Trebuchet_Normal;
    public static CardConfigOptions.TrebuchetAimer TrebuchetAimer => All.TrebuchetAimer_Normal;
    public static CardConfigOptions.ZipZap ZipZap => All.ZipZap_Normal;
    public static CardConfigOptions.OpponentBomb OpponentBomb => All.OpponentBomb_Normal;
    public static CardConfigOptions.OpponentFlagErase OpponentFlagErase => All.OpponentFlagErase_Normal;
    public static CardConfigOptions.OpponentFlagReshuffle OpponentFlagReshuffle => All.OpponentFlagReshuffle_Normal;
    public static CardConfigOptions.Smoke Smoke => All.Smoke_Normal;
    public static CardConfigOptions.Sonar Sonar => All.Sonar_Normal;
    public static CardConfigOptions.MinefieldScout MinefieldScout => All.MinefieldScout_Normal;
    public static CardConfigOptions.ChainReaction ChainReaction => All.ChainReaction_Normal;
    public static CardConfigOptions.FogOfWar FogOfWar => All.FogOfWar_Normal;
    public static CardConfigOptions.Purge Purge => All.Purge_Normal;
}