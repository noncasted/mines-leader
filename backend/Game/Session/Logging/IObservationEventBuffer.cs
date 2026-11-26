namespace Game.Session;

public interface IObservationEventBuffer
{
    int Cursor { get; }
    void Append(string line);
    IReadOnlyList<string> TakeAfter(int exclusiveCursor);
}
