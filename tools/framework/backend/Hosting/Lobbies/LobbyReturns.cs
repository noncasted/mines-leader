using Game.Session;
using Microsoft.Extensions.Logging;
using Shared;

namespace Game.Lobbies;

// LobbyOptions.ReturnsAfterMatch: once per match - when its first player leaves it or when it is closed, whichever comes
// first - the code goes to a new lobby of the game (ILobbyFactory with LobbyCreation.Return) with a place reserved for
// every user of the lobby at the start (SessionCreateOptions.JoinTimeout of the lobby). A join by the code leads there
// from that moment, the players still in the match come later. The new lobby is closed as any lobby (nobody entered
// in time, the last user left) and then releases the code. A lobby the game fails to build releases the code at once:
// the failure is logged, the match goes on.
public class LobbyReturns : ILobbyCodeAfterMatch
{
    public LobbyReturns(ILobbyFactory factory, LobbyOptions options, ILogger<LobbyReturns> logger)
    {
        _factory = factory;
        _maxUsers = options.MaxUsers;
        _logger = logger;
    }

    private readonly ILobbyFactory _factory;
    private readonly int _maxUsers;
    private readonly ILogger<LobbyReturns> _logger;

    public void OnMatchLeft(LobbyMatchCode code)
    {
        Return(code);
    }

    public void OnMatchClosed(LobbyMatchCode code)
    {
        Return(code);
    }

    private void Return(LobbyMatchCode code)
    {
        if (code.ClaimEnd() == false)
            return;

        ISession lobby;

        // The boundary of the return: nobody awaits it, the match that triggered it must not fail with it.
        try
        {
            lobby = Create(code);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "[Lobby] The lobby code {Code} returns to after match {SessionID} is not created: the code is released",
                code.Code,
                code.MatchSessionId);

            code.Release();
            return;
        }

        // Held first: a lobby closed right away releases the code.
        code.ReturnTo(lobby);

        foreach (var userId in code.UserIds)
        {
            if (lobby.Reserve(userId) != SessionReservation.Reserved)
                throw new InvalidOperationException($"Returned lobby session {lobby.Id} rejected the reservation of {userId}");
        }

        _logger.LogInformation("[Lobby] Lobby session {SessionID} with code {Code} returned from match {MatchSessionID} for {Count} users",
            lobby.Id,
            code.Code,
            code.MatchSessionId,
            code.UserIds.Count);
    }

    private ISession Create(LobbyMatchCode code)
    {
        var sessionId = Guid.NewGuid();

        var lobby = _factory.Create(new LobbyCreation
        {
            Id = sessionId,
            Code = code.Code,
            MaxUsers = _maxUsers,
            Return = new LobbyReturn
            {
                LobbySessionId = code.LobbySessionId,
                MatchSessionId = code.MatchSessionId,
                LeaderId = code.LeaderId
            }
        });

        if (lobby.Id != sessionId || lobby.Type != SessionType.Lobby)
            throw new InvalidOperationException($"The lobby factory built {lobby.Type} session {lobby.Id} for returned lobby {sessionId}");

        return lobby;
    }
}
