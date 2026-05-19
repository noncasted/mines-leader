namespace Game.GamePlay;

public class ModifierRoundAction : IRoundAction
{
    public ModifierRoundAction(IPlayer player, IModifierSource source)
    {
        _player = player;
        _source = source;
    }

    private readonly IPlayer _player;
    private readonly IModifierSource _source;

    public bool Tick(MoveSnapshot snapshot)
    {
        var expired = _source.Tick();

        if (expired)
        {
            _player.Modifiers.Remove(snapshot, _source.Id);
            return true;
        }

        _player.Modifiers.Update(snapshot, _source);
        return false;
    }
}
