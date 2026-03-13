namespace Infrastructure;

public interface ISideEffect
{
    Task Execute(IOrleans orleans);
}