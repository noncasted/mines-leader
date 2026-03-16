using Infrastructure.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Execution;

[GenerateSerializer]
public class BatchWriterState<T>
{
    [Id(0)] public readonly List<T> Entries = new();
}

public interface IBatchWriter : IGrainWithStringKey
{
    Task Start();
    Task Loop();
}

public interface IBatchWriter<T> : IBatchWriter
{
}

public class BatchWriterTask<T> : IPriorityTask
{
    public required IBatchWriter<T> Batcher { get; init; }
    public required string Id { get; init; }
    public required TaskPriority Priority { get; init; }
    public TimeSpan Delay { get; set; }

    public Task Execute()
    {
        return Batcher.Loop();
    }
}

public class BatchWriterOptions
{
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(0.1f);
    public bool RequiresTransaction { get; set; } = true;
}

public abstract class BatchWriter<TState, TEntry> : CommonGrain, IBatchWriter<TEntry>
    where TState : BatchWriterState<TEntry>, new()
{
    protected BatchWriter(State<TState> state)
    {
        _state = state;
        _orleans = ServiceProvider.GetRequiredService<IOrleans>();
        _taskScheduler = ServiceProvider.GetRequiredService<ITaskScheduler>();
        _logger = ServiceProvider.GetRequiredService<ILogger<BatchWriter<TState, TEntry>>>();

        _task = new BatchWriterTask<TEntry>
        {
            Id = $"{this.GetPrimaryKeyString()}-{typeof(TState).FullName}",
            Priority = TaskPriority.Low,
            Batcher = this.AsReference<IBatchWriter<TEntry>>()
        };
    }

    private readonly ILogger<BatchWriter<TState, TEntry>> _logger;
    private readonly IOrleans _orleans;
    private readonly Dictionary<Guid, List<TEntry>> _pending = new();

    private readonly State<TState> _state;

    private readonly BatchWriterTask<TEntry> _task;
    private readonly ITaskScheduler _taskScheduler;

    protected abstract BatchWriterOptions Options { get; }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _task.Delay = Options.Delay;
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task Start()
    {
        await _state.Read();

        if (_state.Value.Entries.Count == 0)
            return;

        _taskScheduler.Schedule(_task);
        return;
    }

    public async Task Loop()
    {
        await _state.Read();
        var state = _state.Value;

        if (state.Entries.Count == 0)
            return;

        try
        {
            if (Options.RequiresTransaction == true)
            {
                _orleans.Transactions.Run(() => Process(state.Entries));


                // await _orleans.Transactions.CreateBuilder(() => Process(state.Entries))
                //     .WithSuccessAction(() =>
                //         {
                //             state.Entries.Clear();
                //             return _state.WriteStateAsync();
                //         }
                //     )
                //     .Process();
            }
            else
            {
                await Process(state.Entries);
                state.Entries.Clear();
                await _state.Write();
            }
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "[BatchWriter] Process failed {writerName} {batchType}",
                this.GetPrimaryKeyString(),
                typeof(TEntry).Name
            );

            _taskScheduler.Schedule(_task);
            return;
        }

        if (state.Entries.Count > 0)
            _taskScheduler.Schedule(_task);
    }

    public async Task WriteTransactional(TEntry value)
    {
        var transactionId = TransactionContextProvider.Current!.Id;

        if (_pending.TryGetValue(transactionId, out var list) == false)
        {
            list = new List<TEntry>();
            _pending[transactionId] = list;
        }

        list.Add(value);
        _taskScheduler.Schedule(_task);

        _logger.LogTrace(
            "[BatchWriter] WriteTransactional {writerName} {stateType} {batchType} {transactionId}",
            this.GetPrimaryKeyString(),
            typeof(TState).FullName,
            typeof(TEntry).FullName,
            transactionId
        );
    }

    public async Task WriteDirect(TEntry value)
    {
        await _state.Read();
        _state.Value.Entries.Add(value);
        await _state.Write();
        _taskScheduler.Schedule(_task);

        _logger.LogTrace(
            "[BatchWriter] WriteDirect {writerName} {stateType} {batchType}",
            this.GetPrimaryKeyString(),
            typeof(TState).FullName,
            typeof(TEntry).FullName
        );
    }

    public async Task OnSuccess(Guid transactionId)
    {
        if (_pending.TryGetValue(transactionId, out var pending) == false)
            return;

        await _state.Read();
        _logger.LogTrace(
            "[BatchWriter] OnSuccess {writerName} {stateType} {batchType} {transactionId}",
            this.GetPrimaryKeyString(),
            typeof(TState).FullName,
            typeof(TEntry).Name,
            transactionId
        );

        _state.Value.Entries.AddRange(pending);
        _pending.Remove(transactionId);
        await _state.Write();
        _taskScheduler.Schedule(_task);
    }

    public Task OnFailure(Guid transactionId)
    {
        _logger.LogWarning(
            "[BatchWriter] OnFailure {writerName} {stateType} {batchType} {transactionId}",
            this.GetPrimaryKeyString(),
            typeof(TState).FullName,
            typeof(TEntry).Name,
            transactionId
        );

        _pending.Remove(transactionId);
        return Task.CompletedTask;
    }

    protected abstract Task Process(IReadOnlyList<TEntry> entries);
}