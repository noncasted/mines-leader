namespace Game;

public interface IExecutionQueue
{
    void Enqueue(Action action);
}