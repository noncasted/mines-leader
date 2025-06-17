using Common;
using Game;
using Infrastructure.Messaging;
using Microsoft.Extensions.Hosting;

namespace Backend.Matches;

public class ServerSessions : BackgroundService
{
    public ServerSessions(
        IMessagingClient messaging,
        ISessionFactory sessionFactory,
        ISessionSearch sessionSearch)
    {
        _messaging = messaging;
        _sessionFactory = sessionFactory;
        _sessionSearch = sessionSearch;
    }

    private readonly IMessagingClient _messaging;
    private readonly ISessionFactory _sessionFactory;
    private readonly ISessionSearch _sessionSearch;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lifetime = stoppingToken.ToLifetime();

        _messaging.ListenWithResponse<
            MatchPayloads.Create.Request,
            MatchPayloads.Create.Response>(lifetime, CreateSession);

        _messaging.ListenWithResponse<
            MatchPayloads.GetOrCreate.Request,
            MatchPayloads.GetOrCreate.Response>(lifetime, GetOrCreateSession);

        return Task.CompletedTask;

        MatchPayloads.Create.Response CreateSession(MatchPayloads.Create.Request request)
        {
            var id = _sessionFactory.Create(new SessionCreateOptions
            {
                ExpectedUsers = request.ExpectedUsers,
                Type = request.Type
            });

            return new MatchPayloads.Create.Response
            {
                SessionId = id
            };
        }

        MatchPayloads.GetOrCreate.Response GetOrCreateSession(MatchPayloads.GetOrCreate.Request request)
        {
            var id = _sessionSearch.GetOrCreate(new SessionSearchParameters
            {
                Type = request.Type
            });

            return new MatchPayloads.GetOrCreate.Response
            {
                SessionId = id
            };
        }
    }
}