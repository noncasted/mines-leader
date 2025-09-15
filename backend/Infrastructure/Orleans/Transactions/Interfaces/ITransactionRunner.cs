namespace Infrastructure.Orleans;

public interface ITransactionRunner
{
    public Task Run(Func<Task> action, Func<Task> commitAction);
}