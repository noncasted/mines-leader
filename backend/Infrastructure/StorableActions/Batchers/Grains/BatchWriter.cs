using Common;
using Infrastructure.Orleans;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Transactions;

namespace Infrastructure.StorableActions;

public abstract class BatchWriter<T> : CommonGrain, IBatchWriter<T>, ITransactionHook
{
    protected BatchWriter(IPersistentState<BatchWriterState<T>> state)
    {
        _state = state;
        _orleans = ServiceProvider.GetRequiredService<IOrleans>();
    }

    private readonly IOrleans _orleans;
    private readonly IPersistentState<BatchWriterState<T>> _state;
    private readonly Dictionary<Guid, List<T>> _pending = new();

    public Task Start()
    {
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

    public Task OnSuccess(Guid transactionId)
    {
        _state.State.Entries.AddRange(_pending[transactionId]);
        _pending.Remove(transactionId);
        return _state.WriteStateAsync();
    }

    public Task OnFailure(Guid transactionId)
    {
        _pending.Remove(transactionId);
        return Task.CompletedTask;
    }

    private async Task Loop()
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
            return;
        }
    }

    protected abstract Task Process(IReadOnlyList<T> entries);
}