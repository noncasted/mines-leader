using Common;
using Infrastructure.Orleans;
using Infrastructure.TaskScheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans.Transactions;

namespace Infrastructure.StorableActions;

public class BatchWriterTask<T> : IPriorityTask
{
    public required string Id { get; init; }
    public required TaskPriority Priority { get; init; }
    public required IBatchWriter<T> Batcher { get; init; }

    public Task Execute()
    {
        return Batcher.Loop();
    }
}

public abstract class BatchWriter<T> : CommonGrain, IBatchWriter<T>, ITransactionHook
{
    protected BatchWriter(IPersistentState<BatchWriterState<T>> state)
    {
        _state = state;
        _orleans = ServiceProvider.GetRequiredService<IOrleans>();
        _taskScheduler = ServiceProvider.GetRequiredService<ITaskScheduler>();
        _logger = ServiceProvider.GetRequiredService<ILogger<BatchWriter<T>>>();

        _task = new BatchWriterTask<T>
        {
            Id = this.GetPrimaryKeyString(),
            Priority = TaskPriority.Low,
            Batcher = this
        };
    }

    private readonly ILogger<BatchWriter<T>> _logger;
    private readonly IOrleans _orleans;
    private readonly ITaskScheduler _taskScheduler;

    private readonly IPersistentState<BatchWriterState<T>> _state;
    private readonly Dictionary<Guid, List<T>> _pending = new();

    private readonly IPriorityTask _task;

    public Task Start()
    {
        if (_state.State.Entries.Count == 0)
            return Task.CompletedTask;

        _taskScheduler.Schedule(_task);
        return Task.CompletedTask;
    }

    public Task Write(T value)
    {
        this.AsTransactionHook();

        var transactionId = TransactionContext.GetRequiredTransactionInfo().TransactionId;
        if (_pending.TryGetValue(transactionId, out var list) == false)
        {
            list = new List<T>();
            _pending[transactionId] = list;
        }

        list.Add(value);
        return Task.CompletedTask;
    }

    public async Task OnSuccess(Guid transactionId)
    {
        _state.State.Entries.AddRange(_pending[transactionId]);
        _pending.Remove(transactionId);
        await _state.WriteStateAsync();
        _taskScheduler.Schedule(_task);
    }

    public Task OnFailure(Guid transactionId)
    {
        _pending.Remove(transactionId);
        return Task.CompletedTask;
    }

    public async Task Loop()
    {
        var state = _state.State;

        if (state.Entries.Count == 0)
            return;

        try
        {
            await _orleans
                .RunTransaction(() => Process(state.Entries))
                .WithSuccessAction(() =>
                    {
                        state.Entries.Clear();
                        return _state.WriteStateAsync();
                    }
                )
                .Start();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[BatchWriter] Process failed {writerName} {batchType}", 
                this.GetPrimaryKeyString(),
                typeof(T).Name
            );
            
            _taskScheduler.Schedule(_task);
            return;
        }

        if (state.Entries.Count > 0)
            _taskScheduler.Schedule(_task);
    }

    protected abstract Task Process(IReadOnlyList<T> entries);
}