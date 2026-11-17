using Microsoft.Extensions.Logging;

namespace Game.Session;

// The log of the session core on the providers of the host.
public class SessionLog : ISessionLog
{
    public SessionLog(ILogger<SessionLog> logger)
    {
        _logger = logger;
    }

    private readonly ILogger<SessionLog> _logger;

    public void Debug(string message)
    {
        _logger.LogDebug("{Message}", message);
    }

    public void Info(string message)
    {
        _logger.LogInformation("{Message}", message);
    }

    public void Warning(string message)
    {
        _logger.LogWarning("{Message}", message);
    }

    public void Error(string message, Exception? exception = null)
    {
        _logger.LogError(exception, "{Message}", message);
    }
}
