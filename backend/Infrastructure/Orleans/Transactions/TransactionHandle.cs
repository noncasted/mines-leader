using Microsoft.Extensions.Logging;
using Orleans.Concurrency;
using Orleans.Placement;

namespace Infrastructure;

public interface ITransactionHandle : IGrainWithGuidKey
{
    Task AddListener(ITransactionHook hook);
    Task OnSuccess();
    Task OnFailure();
}

[Reentrant]
[PreferLocalPlacement]
public class TransactionHandle : Grain, ITransactionHandle
{
    public TransactionHandle(ILogger<TransactionHandle> logger)
    {
        _logger = logger;
    }

    private readonly ILogger<TransactionHandle> _logger;
    private readonly List<ITransactionHook> _hooks = new();

    private bool _isCompleted;

    public Task AddListener(ITransactionHook hook)
    {
        if (_isCompleted == true)
        {
            throw new Exception(
                $"[Transaction] [Handle] Cannot add hook to transaction {this.GetPrimaryKey()} because it is already completed"
            );
        }        
        
        _hooks.Add(hook);

        _logger.LogTrace(
            "[Transaction] [Handle] Adding hook to transaction {TransactionId} current hook count {_hookCount}",
            this.GetPrimaryKey(),
            _hooks.Count
        );

        return Task.CompletedTask;
    }

    public Task OnSuccess()
    {
        if (_hooks.Count == 0)
            throw new Exception($"[Transaction] [Handle] Transaction {this.GetPrimaryKey()} has no hooks to notify on success");
        
        _isCompleted = true;
        
        _logger.LogTrace(
            "[Transaction] [Handle] Transaction {TransactionId} succeeded, notifying {_hookCount} hooks",
            this.GetPrimaryKey(), _hooks.Count
        );

        return Task.WhenAll(_hooks.Select(t => t.OnSuccess(this.GetPrimaryKey())));
    }

    public Task OnFailure()
    {
        if (_hooks.Count == 0)
            throw new Exception($"[Transaction] [Handle] Transaction {this.GetPrimaryKey()} has no hooks to notify on failure");
        
        _isCompleted = true;
   
        _logger.LogTrace(
            "[Transaction] [Handle] Transaction {TransactionId} failed, notifying {_hookCount} hooks",
            this.GetPrimaryKey(), _hooks.Count
        );

        return Task.WhenAll(_hooks.Select(t => t.OnFailure(this.GetPrimaryKey())));
    }
}