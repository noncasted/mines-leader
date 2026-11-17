using Internal;

namespace Common.Extensions;

// Copy of the framework OperationProgress (removed from the framework in 0.7.0): the progress of a benchmark run,
// shown by the console.
public interface IOperationProgress
{
    DateTime StartTime { get; }
    DateTime EndTime { get; }

    IViewableProperty<float> Progress { get; }
    IViewableProperty<OperationStatus> Status { get; }
    IViewableProperty<string> Message { get; }

    void SetProgress(float progress);
    void Log(string message);
    void SetStatus(OperationStatus status);
}

public enum OperationStatus
{
    NotStarted = 0,
    Preparing = 10,
    InProgress = 20,
    Success = 30,
    Failed = 40,
    Cancelled = 50
}

public class OperationProgress : IOperationProgress
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ViewableProperty<string> _message = new(string.Empty);

    private readonly ViewableProperty<float> _progress = new(0);
    private readonly ViewableProperty<OperationStatus> _status = new(OperationStatus.NotStarted);

    public DateTime StartTime { get; private set; }

    public DateTime EndTime { get; private set; }

    public IViewableProperty<float> Progress => _progress;
    public IViewableProperty<OperationStatus> Status => _status;
    public IViewableProperty<string> Message => _message;

    public void SetProgress(float progress)
    {
        _progress.Set(progress);
    }

    public void Log(string message)
    {
        _message.Set(message);
    }

    // The times are written under the lock, the value is set outside it: a subscriber that throws does not hang the
    // next SetStatus.
    public void SetStatus(OperationStatus status)
    {
        _lock.Wait();

        try
        {
            if (status == OperationStatus.Preparing)
                StartTime = DateTime.UtcNow;

            if (status is OperationStatus.Success or OperationStatus.Failed or OperationStatus.Cancelled)
                EndTime = DateTime.UtcNow;
        }
        finally
        {
            _lock.Release();
        }

        _status.Set(status);
    }
}
