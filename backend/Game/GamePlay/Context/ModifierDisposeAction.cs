using Shared;

namespace Game.GamePlay;

public class ModifierDisposeAction : IRoundAction
{
    public ModifierDisposeAction(IPlayer player, PlayerModifier type, float amount)
    {
        _player = player;
        _type = type;
        _amount = amount;
    }

    private readonly IPlayer _player;
    private readonly PlayerModifier _type;
    private readonly float _amount;

    public void Execute(MoveSnapshot snapshot)
    {
        _player.Modifiers.Dec(snapshot, _type, _amount);
    }
}
