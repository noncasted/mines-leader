using System.Globalization;
using Infrastructure;
using Shared;

namespace Game.Session;

public class SessionFileLogger : ISessionLogger, IDisposable
{
    public SessionFileLogger(ISessionData sessionData)
    {
        _sessionId = sessionData.Id;
        _writer = CreateWriter(sessionData.Id);

        sessionData.Lifetime.Listen(Dispose);
    }

    private readonly Guid _sessionId;
    private readonly StreamWriter _writer;
    private readonly object _lock = new();
    private readonly Dictionary<Guid, string> _playerLabels = new();
    private bool _disposed;

    public void RegisterPlayers(IReadOnlyList<IUser> users)
    {
        var botCount = users.Count(u => u.IsBot);
        var hasBots = botCount > 0;

        foreach (var user in users)
        {
            if (hasBots)
                _playerLabels[user.Id] = user.IsBot ? "Bot" : "Human";
            else
                _playerLabels[user.Id] = $"Player{user.Index}";
        }
    }

    private string Label(Guid playerId)
    {
        return _playerLabels.TryGetValue(playerId, out var label) ? label : playerId.ToString();
    }

    public void LogSessionCreated(SessionType type, Guid sessionId)
    {
        Write($"[Session] Created | Type={type} | Id={sessionId}");
    }

    public void LogGameStarted(IReadOnlyList<Guid> playerIds)
    {
        var labels = playerIds.Select(id => $"{Label(id)}({id})");
        Write($"[Game] Started | Players=[{string.Join(", ", labels)}]");
    }

    public void LogRoundStart(Guid playerId, int roundNumber)
    {
        Write($"-------- Round {roundNumber} Start {Label(playerId)} --------");
    }

    public void LogRoundEnd(Guid playerId, int roundNumber)
    {
        Write($"-------- Round {roundNumber} End {Label(playerId)} --------");
    }

    public void LogCellOpened(Guid playerId, Position position, bool hasMine, bool shieldConsumed)
    {
        var result = hasMine ? (shieldConsumed ? "Mine (shield absorbed)" : "Mine (damage)") : "Safe";
        Write($"[Cell] Opened | Player={Label(playerId)} | Pos={position} | Result={result}");
    }

    public void LogFlagSet(Guid playerId, Position position)
    {
        Write($"[Flag] Set | Player={Label(playerId)} | Pos={position}");
    }

    public void LogFlagRemoved(Guid playerId, Position position)
    {
        Write($"[Flag] Removed | Player={Label(playerId)} | Pos={position}");
    }

    public void LogCardUsed(Guid playerId, CardType cardType, int manaCost, bool success)
    {
        Write($"[Card] Used | Player={Label(playerId)} | Type={cardType} | ManaCost={manaCost} | Success={success}");
    }

    public void LogHealthChanged(Guid playerId, int oldHealth, int newHealth)
    {
        var delta = newHealth - oldHealth;
        var sign = delta >= 0 ? "+" : "";
        Write($"[Health] Changed | Player={Label(playerId)} | {oldHealth} -> {newHealth} ({sign}{delta})");
    }

    public void LogManaChanged(Guid playerId, int current, int max)
    {
        Write($"[Mana] Changed | Player={Label(playerId)} | {current}/{max}");
    }

    public void LogBotTurnStart(Guid botId)
    {
        Write($"[Bot] Turn start | {Label(botId)}");
    }

    public void LogBotAction(string actionType, string details)
    {
        Write($"[Bot] {actionType} | {details}");
    }

    public void LogBotProfile(Guid botId, string profile)
    {
        Write($"[Bot] Profile | {Label(botId)} | Profile={profile}");
    }

    public void LogTurnSkipped(Guid playerId)
    {
        Write($"[Turn] Skipped | Player={Label(playerId)}");
    }

    public void LogGameOver(Guid winnerId, string reason)
    {
        Write($"[Game] Over | Winner={Label(winnerId)} | Reason={reason}");
    }

    public void Log(string message)
    {
        Write(message);
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                _writer.Flush();
                _writer.Dispose();
            }
            catch
            {
                // Ignore dispose errors
            }
        }
    }

    private void Write(string message)
    {
        var timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var line = $"[{timestamp}] {message}";

        lock (_lock)
        {
            if (_disposed)
                return;

            try
            {
                _writer.WriteLine(line);
            }
            catch
            {
                // Ignore write errors — logging must not crash the game
            }
        }
    }

    private static StreamWriter CreateWriter(Guid sessionId)
    {
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var dir = TelemetryPaths.GetTelemetryDir("logs-games");

        var directory = dir != null
            ? Path.Combine(dir, date)
            : Path.Combine(AppContext.BaseDirectory, "logs", "sessions", date);
        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(directory, $"{sessionId}.log");
        var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        return new StreamWriter(stream) { AutoFlush = true };
    }
}