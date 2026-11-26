using Cluster.Lobbies;
using Common.Extensions;
using Game.Session;
using Internal;
using Microsoft.Extensions.Logging;

namespace Game.Lobbies;

// Who holds the code of a lobby of this game server and when the code goes back to the registry.
// - A lobby that has not started holds its code itself: the code is released when the lobby is closed.
// - At zero of the start countdown the code passes to the match the lobby has created: the registry entry is
//   marked as started (a join by the code leads to the lobby of the late players of the match). The lobby session,
//   closed about a second after zero, does not touch the code any more. What becomes of the code after the match is
//   ILobbyCodeAfterMatch: it is told when the first player leaves the match and when the match is closed - its last
//   player left, the game ended, nobody entered in time - and releases the code (LobbyCodeRelease) or returns it to a
//   new lobby (LobbyReturns, LobbyOptions.ReturnsAfterMatch), which then holds it as any lobby.
// The registry calls of one code (mark, listing reports, return, release) go one after another, in the order they are
// made here, across the lobbies the code goes through: a report never overtakes the mark, the release is the last call.
// Of the reports queued behind a slow call only the latest one is sent.
// A game server that died keeps its codes in the registry until the next deploy, started or not.
public interface ILobbyCodeLeases
{
    void Hold(string code, ISession lobby);

    // Called from the queue of the lobby session, while the lobby is alive. leaderId and userIds: the leader and the
    // users of the lobby at the start (ILobbyMatches.Create gets them) - the lobby the code returns to needs them.
    void PassToMatch(Guid lobbySessionId, ISession match, Guid leaderId, IReadOnlyList<Guid> userIds);

    // The code of the lobby leads to its running match. False for a lobby that has not started or whose code is
    // released or returned; whether the match takes a new player now is up to ILobbyEntryPolicy.
    bool IsPassedToMatch(Guid lobbySessionId);
}

public class LobbyCodeLeases : ILobbyCodeLeases, ILobbyListings
{
    public LobbyCodeLeases(ILobbyCodes codes, ILobbyCodeAfterMatch afterMatch, ILogger<LobbyCodeLeases> logger)
    {
        _codes = codes;
        _afterMatch = afterMatch;
        _logger = logger;
    }

    private readonly ILobbyCodes _codes;
    private readonly ILobbyCodeAfterMatch _afterMatch;
    private readonly ILogger<LobbyCodeLeases> _logger;

    // Lobby session -> its lease; an entry lives until the lobby is closed and the code is not its own any more.
    private readonly Dictionary<Guid, LobbyCodeLease> _leases = new();
    private readonly Lock _lock = new();

    public void Hold(string code, ISession lobby)
    {
        lock (_lock)
            _leases.Add(lobby.Id, new LobbyCodeLease(code, lobby.Id, Task.CompletedTask));

        ListenClose(lobby);
    }

    public void PassToMatch(Guid lobbySessionId, ISession match, Guid leaderId, IReadOnlyList<Guid> userIds)
    {
        LobbyCodeLease? lease;

        lock (_lock)
        {
            if (_leases.TryGetValue(lobbySessionId, out lease) == false)
                throw new InvalidOperationException($"Lobby session {lobbySessionId} holds no code");

            lease.PassToMatch();
            Enqueue(lease, () => _codes.MarkStarted(lease.Code, lobbySessionId, match.Id));

            // The lobby is not shown any more; the match shows itself once its players are in.
            ReportLocked(lease, new LobbyListing
            {
                IsOpen = lease.Listing?.IsOpen ?? false,
                MaxPlayers = lease.Listing?.MaxPlayers ?? 0,
                IsRunning = true
            });
        }

        _logger.LogInformation("[Lobby] Code {Code} of lobby session {LobbySessionID} is held for match {SessionID}",
            lease.Code,
            lobbySessionId,
            match.Id);

        var code = new LobbyMatchCode(this, lease, match.Id, leaderId, userIds.ToList());

        match.Lifetime.Listen(() => _afterMatch.OnMatchClosed(code));

        // The users of a session are added and removed in its queue. A seat ends when its user leaves for good (a lost
        // connection waits for its return); the seats end with the match too.
        match.ExecutionQueue.Enqueue(() => match.Users.View(match.Lifetime, (_, user) => {
            if (user.IsBot == false)
                user.Seat.Lifetime.Listen(() => OnMatchLeft(lease, code));
        }));
    }

    public bool IsPassedToMatch(Guid lobbySessionId)
    {
        lock (_lock)
        {
            if (_leases.TryGetValue(lobbySessionId, out var lease) == false ||
                lease.IsPassed == false ||
                lease.IsReleased == true)
                return false;

            return true;
        }
    }

    public void Report(Guid lobbySessionId, LobbyListing listing)
    {
        lock (_lock)
        {
            // The code is released or returned (a player left the match at the moment it was closed): nothing to show.
            if (_leases.TryGetValue(lobbySessionId, out var lease) == false || lease.IsReleased == true)
                return;

            ReportLocked(lease, listing);
        }
    }

    internal bool ClaimMatchEnd(LobbyCodeLease lease)
    {
        lock (_lock)
            return lease.TryClaimMatchEnd();
    }

    internal void ReleaseFromMatch(LobbyCodeLease lease, Guid matchSessionId)
    {
        lock (_lock)
        {
            // The lobby session is usually closed long before; if not, it forgets the lease when it is.
            if (lease.IsLobbyClosed == true)
                _leases.Remove(lease.LobbySessionId);

            Release(lease);
        }

        _logger.LogInformation("[Lobby] Code {Code} is released: match {SessionID} is over", lease.Code, matchSessionId);
    }

    internal void ReturnFromMatch(LobbyCodeLease lease, ISession lobby)
    {
        lock (_lock)
        {
            if (lease.IsLobbyClosed == true)
                _leases.Remove(lease.LobbySessionId);

            var returned = lease.HandOver(lobby.Id);
            _leases.Add(lobby.Id, returned);

            Enqueue(returned, () => _codes.MarkReturned(lease.Code, lease.LobbySessionId, lobby.Id));
        }

        ListenClose(lobby);

        _logger.LogInformation("[Lobby] Code {Code} of lobby session {LobbySessionID} is returned to lobby session {SessionID}",
            lease.Code,
            lease.LobbySessionId,
            lobby.Id);
    }

    private void ListenClose(ISession lobby)
    {
        var lobbySessionId = lobby.Id;

        // The lifetime of a session is terminated exactly once, when it is closed.
        lobby.Lifetime.Listen(() => OnLobbyClosed(lobbySessionId));
    }

    private void OnMatchLeft(LobbyCodeLease lease, LobbyMatchCode code)
    {
        lock (_lock)
        {
            if (lease.TryMarkMatchLeft() == false)
                return;
        }

        _afterMatch.OnMatchLeft(code);
    }

    // Under the lock.
    private void ReportLocked(LobbyCodeLease lease, LobbyListing listing)
    {
        if (lease.QueueReport(listing) == false)
            return;

        Enqueue(lease, () => {
            LobbyListing latest;

            lock (_lock)
                latest = lease.TakeReport();

            return _codes.UpdateListing(lease.Code, lease.LobbySessionId, latest);
        });
    }

    private void OnLobbyClosed(Guid lobbySessionId)
    {
        lock (_lock)
        {
            if (_leases.TryGetValue(lobbySessionId, out var lease) == false)
                throw new InvalidOperationException($"Closed lobby session {lobbySessionId} holds no code");

            lease.MarkLobbyClosed();

            // The match of the lobby holds the code: the lease lives until the match is done with it.
            if (lease.IsPassed == true)
            {
                if (lease.IsReleased == true)
                    _leases.Remove(lobbySessionId);

                return;
            }

            _leases.Remove(lobbySessionId);
            Release(lease);
        }
    }

    // Under the lock.
    private void Release(LobbyCodeLease lease)
    {
        lease.MarkReleased();
        Enqueue(lease, () => _codes.Release(lease.Code, lease.LobbySessionId));
    }

    // Under the lock. A failed call is logged and does not stop the calls after it.
    private void Enqueue(LobbyCodeLease lease, Func<Task> call)
    {
        lease.Chain(Run).NoAwait();

        async Task Run(Task previous)
        {
            await previous;

            try
            {
                await call();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[Lobby] Registry call for code {Code} failed", lease.Code);
            }
        }
    }
}
