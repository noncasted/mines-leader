using Common.Extensions;
using Game.Session;
using Internal;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Lobbies;

// The countdown of the start of a lobby: follows the start flag of the leader, one step on the clock of the server,
// at zero the match starts.
internal class LobbyCountdown
{
    public LobbyCountdown(
        LobbyProperties properties,
        LobbyMatchStart matchStart,
        IExecutionQueue executionQueue,
        TimeProvider time,
        TimeSpan step,
        ILogger logger)
    {
        _properties = properties;
        _matchStart = matchStart;
        _executionQueue = executionQueue;
        _time = time;
        _step = step;
        _logger = logger;
    }

    private readonly LobbyProperties _properties;
    private readonly LobbyMatchStart _matchStart;
    private readonly IExecutionQueue _executionQueue;
    private readonly TimeProvider _time;
    private readonly TimeSpan _step;
    private readonly ILogger _logger;

    private ILifetime? _lifetime;

    public void OnStartWritten(IReadOnlyLifetime serviceLifetime, LobbyStartState state)
    {
        // "Cancel" that came after zero: the match exists, the users are on their way to it.
        if (_properties.IsGameStarted == true)
            return;

        if (state.IsStarting == _properties.Countdown.Value.IsActive)
            return;

        if (state.IsStarting == true)
            Start(serviceLifetime);
        else
            Stop();
    }

    public void Stop()
    {
        _logger.LogInformation("[Lobby] Start countdown is cancelled at {Seconds} s", _properties.Countdown.Value.SecondsLeft);

        _lifetime!.Terminate();
        _lifetime = null;

        _properties.Countdown.Set(new LobbyCountdownState());
    }

    private void Start(IReadOnlyLifetime serviceLifetime)
    {
        var lifetime = serviceLifetime.Child();
        _lifetime = lifetime;

        _logger.LogInformation("[Lobby] Start countdown: {Seconds} s", LobbyCountdownState.Seconds);

        _properties.Countdown.Set(new LobbyCountdownState
        {
            IsActive = true,
            SecondsLeft = LobbyCountdownState.Seconds
        });

        ScheduleStep(lifetime);
    }

    private void ScheduleStep(IReadOnlyLifetime lifetime)
    {
        Wait().NoAwait();

        async Task Wait()
        {
            // Cancelled with the countdown (a cancel, the end of the session): nothing waits for a dead step.
            await Task.Delay(_step, _time, lifetime.Token).ConfigureAwait(false);
            _executionQueue.Enqueue(() => OnStep(lifetime));
        }
    }

    private void OnStep(IReadOnlyLifetime lifetime)
    {
        // cancelled (or the session is closed) while the step was waiting
        if (lifetime.IsTerminated == true)
            return;

        var secondsLeft = _properties.Countdown.Value.SecondsLeft - 1;

        _properties.Countdown.Set(new LobbyCountdownState
        {
            IsActive = true,
            SecondsLeft = secondsLeft
        });

        if (secondsLeft > 0)
        {
            ScheduleStep(lifetime);
            return;
        }

        _lifetime!.Terminate();
        _lifetime = null;

        _matchStart.Start();
    }
}
