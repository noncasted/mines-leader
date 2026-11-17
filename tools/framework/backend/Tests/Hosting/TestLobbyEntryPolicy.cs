using Cluster.Deploy;
using Cluster.Discovery;
using Cluster.Lobbies;
using Cluster.Monitoring;
using Common.Network;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Session;
using Game.Hosting;
using Game.Hosting.Collection;
using Game.Lobbies;
using Game.Session;
using Internal;
using MemoryPack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared;

namespace Framework.Tests.Hosting;

// When a running match lets a late player in: the tests switch it.
public class TestLobbyEntryPolicy : ILobbyEntryPolicy
{
    // The matches that have ended (NotFound for their late players).
    private readonly HashSet<Guid> _overMatches = new();

    // False: no running match can be entered now (Started).
    private bool _canEnter = true;

    public void SetCanEnter(bool canEnter)
    {
        _canEnter = canEnter;
    }

    public void MarkMatchOver(Guid matchSessionId)
    {
        lock (_overMatches)
            _overMatches.Add(matchSessionId);
    }

    public bool IsMatchOver(Guid matchSessionId)
    {
        lock (_overMatches)
            return _overMatches.Contains(matchSessionId);
    }

    public bool CanEnterMatchNow(Guid matchSessionId)
    {
        return _canEnter;
    }
}
