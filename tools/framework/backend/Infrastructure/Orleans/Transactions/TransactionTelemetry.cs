using System.Diagnostics;
using System.Diagnostics.Metrics;
using Common.Extensions;

namespace Infrastructure;

public static class TransactionTelemetry
{
    public static readonly ActivitySource Source = new("Infrastructure.Transactions");

    public static readonly Counter<long> Total = BackendMeter.Instance.CreateCounter<long>(
        "backend.transactions.total", description: "Total transactions processed");

    public static readonly Counter<long> Success = BackendMeter.Instance.CreateCounter<long>(
        "backend.transactions.success", description: "Successful transactions");

    public static readonly Counter<long> Failure = BackendMeter.Instance.CreateCounter<long>(
        "backend.transactions.failure", description: "Failed transactions");

    public static readonly Counter<long> Rollback = BackendMeter.Instance.CreateCounter<long>(
        "backend.transactions.rollback", description: "Transaction rollbacks");

    public static readonly Counter<long> RollbackFailure = BackendMeter.Instance.CreateCounter<long>(
        "backend.transactions.rollback_failure", description: "Failed transaction rollbacks");

    public static readonly Counter<long> ConfirmFailure = BackendMeter.Instance.CreateCounter<long>(
        "backend.transactions.confirm_failure", description: "Participants failed to confirm a committed transaction");

    public static readonly Histogram<double> Duration = BackendMeter.Instance.CreateHistogram<double>(
        "backend.transactions.duration", "ms", "Transaction duration");

    public static readonly Histogram<int> ParticipantCount = BackendMeter.Instance.CreateHistogram<int>(
        "backend.transactions.participant_count", description: "Participants per transaction");
}
