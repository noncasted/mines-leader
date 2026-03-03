using Microsoft.Extensions.Logging;
using Orleans.Transactions;
using Orleans.Transactions.Abstractions;

namespace Infrastructure;

public interface ITransactionResolver : ITransactionAgent
{
    Task<(TransactionalStatus, Exception?)> Resolve(TransactionInfo transactionInfo);
}

public class TransactionResolver : ITransactionResolver
{
    private readonly ILogger _logger;
    private readonly CausalClock _clock;
    private readonly ITransactionAgentStatistics _statistics;
    private readonly ITransactionOverloadDetector _overloadDetector;
    private readonly ReadCommiter _readCommiter;
    private readonly WriteCommiter _writeCommiter;

    public TransactionResolver(
        IClock clock,
        IGrainFactory grains,
        ITransactionAgentStatistics statistics,
        ITransactionOverloadDetector overloadDetector,
        ILogger<TransactionResolver> logger)
    {
        _clock = new CausalClock(clock);
        _logger = logger;
        _statistics = statistics;
        _overloadDetector = overloadDetector;
        _readCommiter = new ReadCommiter(grains, logger);
        _writeCommiter = new WriteCommiter(grains, logger);
    }

    public Task<TransactionInfo> StartTransaction(bool readOnly, TimeSpan timeout)
    {
        if (_overloadDetector.IsOverloaded() == true)
        {
            _statistics.TrackTransactionThrottled();
            throw new OrleansStartTransactionFailedException(new OrleansTransactionOverloadException());
        }

        var guid = Guid.NewGuid();
        var ts = _clock.UtcNow();

        _statistics.TrackTransactionStarted();
        return Task.FromResult(new TransactionInfo(guid, ts, ts));
    }

    public async Task<(TransactionalStatus, Exception?)> Resolve(TransactionInfo transactionInfo)
    {
        _logger.LogTrace("[Transaction] Resolving transaction {TransactionInfo}", transactionInfo);

        transactionInfo.TimeStamp = _clock.MergeUtcNow(transactionInfo.TimeStamp);

        if (transactionInfo.Participants.Count == 0)
        {
            _logger.LogTrace("[Transaction] No participants found for transaction {TransactionInfo}", transactionInfo);
            _statistics.TrackTransactionSucceeded();
            return (TransactionalStatus.Ok, null);
        }

        var participants = new TransactionParticipants(transactionInfo);
        participants.Collect();

        try
        {
            var (status, exception) = participants.Write.Count switch
            {
                0 => await _readCommiter.Execute(participants),
                _ => await _writeCommiter.Execute(participants)
            };

            if (status == TransactionalStatus.Ok)
                _statistics.TrackTransactionSucceeded();
            else
                _statistics.TrackTransactionFailed();

            _logger.LogTrace("[Transaction] Resolved transaction {TransactionInfo} with status {Status}",
                transactionInfo, status
            );
            return (status, exception);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Transaction] Resolve failed for transaction {TransactionInfo}", transactionInfo);
            _statistics.TrackTransactionFailed();
            throw;
        }
    }

    public async Task Abort(TransactionInfo transactionInfo)
    {
        _statistics.TrackTransactionFailed();

        var participants = transactionInfo.Participants.Keys.ToList();

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Abort {TransactionInfo} {Participants}", transactionInfo,
                string.Join(",", participants.Select(p => p.ToString()))
            );
        }

        // send one-way abort messages to release the locks and roll back any updates
        await Task.WhenAll(participants.Select(p =>
                {
                    var resourceExtension = p.AsResource();
                    return resourceExtension
                        .Abort(p.Name, transactionInfo.TransactionId);
                }
            )
        );
    }
}