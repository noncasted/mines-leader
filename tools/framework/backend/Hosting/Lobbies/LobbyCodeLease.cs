using Cluster.Lobbies;

namespace Game.Lobbies;

// The code held by one lobby session (LobbyCodeLeases): by the lobby, then by its match, until it is released or the
// match hands it to the lobby it returns to. Changed only under the lock of the leases.
internal class LobbyCodeLease
{
    // tail: the last registry call of the code made before this lease (the lease of the lobby the code comes from).
    public LobbyCodeLease(string code, Guid lobbySessionId, Task tail)
    {
        Code = code;
        LobbySessionId = lobbySessionId;
        _tail = tail;
    }

    public string Code { get; }
    public Guid LobbySessionId { get; }

    public bool IsPassed { get; private set; }
    public bool IsLobbyClosed { get; private set; }

    // The lease does not hold the code any more: released, or handed to the lobby the match has returned it to.
    public bool IsReleased { get; private set; }

    // The last reported listing (of the lobby, then of its match); null until the lobby reports itself.
    public LobbyListing? Listing { get; private set; }

    // A player of the match has left it: told once.
    private bool _isMatchLeft;

    // What becomes of the code after the match is decided: once.
    private bool _isMatchEndClaimed;

    private bool _isReportQueued;

    // The last registry call of the code.
    private Task _tail;

    public void PassToMatch()
    {
        if (IsPassed == true)
            throw new InvalidOperationException($"The code of lobby session {LobbySessionId} is already passed");

        IsPassed = true;
    }

    public void MarkLobbyClosed()
    {
        IsLobbyClosed = true;
    }

    public void MarkReleased()
    {
        if (IsReleased == true)
            throw new InvalidOperationException($"The code {Code} of lobby session {LobbySessionId} is already released");

        IsReleased = true;
    }

    // The lease of the lobby the code goes to: its registry calls go after the calls of this one.
    public LobbyCodeLease HandOver(Guid lobbySessionId)
    {
        MarkReleased();
        return new LobbyCodeLease(Code, lobbySessionId, _tail);
    }

    // True for the first leave only.
    public bool TryMarkMatchLeft()
    {
        if (_isMatchLeft == true)
            return false;

        _isMatchLeft = true;
        return true;
    }

    // True for the first caller only.
    public bool TryClaimMatchEnd()
    {
        if (_isMatchEndClaimed == true)
            return false;

        _isMatchEndClaimed = true;
        return true;
    }

    // The listing to send; true when no report is queued yet (the queued one sends the latest listing).
    public bool QueueReport(LobbyListing listing)
    {
        Listing = listing;

        if (_isReportQueued == true)
            return false;

        _isReportQueued = true;
        return true;
    }

    public LobbyListing TakeReport()
    {
        _isReportQueued = false;
        return Listing!;
    }

    // The next registry call runs after the previous one.
    public Task Chain(Func<Task, Task> call)
    {
        _tail = call(_tail);
        return _tail;
    }
}
