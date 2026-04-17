using Shared;

namespace Game.GamePlay;

public interface IModifiers
{
    IReadOnlyDictionary<PlayerModifier, float> Values { get; }

    void Set(MoveSnapshot snapshot, PlayerModifier type, float value);
}

public class Modifiers : IModifiers
{
    public Modifiers()
    {
        foreach (var type in PlayerModifierExtensions.All)
            _values[type] = 0f;
    }

    private readonly Dictionary<PlayerModifier, float> _values = new();
    private IPlayer? _owner;

    public IReadOnlyDictionary<PlayerModifier, float> Values => _values;

    public void BindOwner(IPlayer owner)
    {
        _owner = owner;
    }

    public void Set(MoveSnapshot snapshot, PlayerModifier type, float value)
    {
        _values[type] = value;

        if (_owner == null)
            return;

        snapshot.RecordModifierUpdate(_owner, type, value);

        switch (type)
        {
            case PlayerModifier.AdditionalMana:
                snapshot.RecordManaUpdate(_owner);
                break;
            case PlayerModifier.AdditionalHealth:
                snapshot.RecordHealthUpdate(_owner);
                break;
            case PlayerModifier.AdditionalMoves:
                snapshot.RecordMovesUpdate(_owner);
                break;
        }
    }
}

public static class PlayerModifiersExtensions
{
    extension(IModifiers modifiers)
    {
        public float Get(PlayerModifier type)
        {
            return modifiers.Values[type];
        }

        public void Inc(MoveSnapshot snapshot, PlayerModifier type)
        {
            modifiers.Set(snapshot, type, modifiers.Values[type] + 1);
        }

        public void Inc(MoveSnapshot snapshot, PlayerModifier type, float amount)
        {
            modifiers.Set(snapshot, type, modifiers.Values[type] + amount);
        }

        public void Dec(MoveSnapshot snapshot, PlayerModifier type, float amount)
        {
            modifiers.Set(snapshot, type, modifiers.Values[type] - amount);
        }

        public void Reset(MoveSnapshot snapshot, PlayerModifier type)
        {
            modifiers.Set(snapshot, type, 0f);
        }
    }
}