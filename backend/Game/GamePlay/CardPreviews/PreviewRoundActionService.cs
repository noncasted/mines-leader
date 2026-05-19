namespace Game.GamePlay.CardPreviews;

/// <summary>
/// No-op <see cref="IRoundActionService"/> for preview generation.
/// Effect cards (Smoke/Blackout/Frost/Fog/ThermalVision) schedule a dispose action
/// to remove their cell effects after N rounds — for a static preview we keep the
/// effect visible in the captured snapshot and never tick rounds, so <see cref="Schedule"/>
/// is intentionally a no-op.
/// </summary>
internal sealed class PreviewRoundActionService : IRoundActionService
{
    public void Schedule(IRoundAction action)
    {
    }

    public void Tick(MoveSnapshot snapshot)
    {
    }
}