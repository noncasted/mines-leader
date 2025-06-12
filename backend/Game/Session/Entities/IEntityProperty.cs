namespace Game;

public interface IEntityProperty
{
    int Id { get; }
    bool IsDirty { get; }
    byte[] Value { get; }

    void Update(byte[] value);
    void MarkClean();
}