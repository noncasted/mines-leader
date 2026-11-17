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

// The registry grain without the cluster: same rules (LobbyRegistry), one dictionary.
public class FakeLobbyCodes : ILobbyCodes
{
    private readonly Dictionary<string, LobbyRegistryEntry> _entries = new();

    // Every registry call in the order the registry got them: "Mark CODE", "Listing CODE", "Release CODE".
    private readonly List<string> _calls = new();

    // Null: real random codes.
    private Func<string>? _generator;

    // Not null: the registry answers MarkStarted only when the gate is completed (a slow grain call).
    public TaskCompletionSource? MarkStartedGate { get; private set; }

    // Copies, taken under the lock of the registry.
    public IReadOnlyDictionary<string, LobbyRegistryEntry> Entries
    {
        get
        {
            lock (_entries)
                return new Dictionary<string, LobbyRegistryEntry>(_entries);
        }
    }

    public IReadOnlyList<string> Calls
    {
        get
        {
            lock (_entries)
                return _calls.ToList();
        }
    }

    public void SetGenerator(Func<string> generator)
    {
        _generator = generator;
    }

    public void HoldMarkStarted(TaskCompletionSource gate)
    {
        MarkStartedGate = gate;
    }

    public Task UpdateListing(string code, Guid sessionId, LobbyListing listing)
    {
        lock (_entries)
        {
            if (_entries.TryGetValue(code, out var entry) == false || entry.SessionId != sessionId)
                throw new InvalidOperationException($"Lobby code {code} is not held by session {sessionId}");

            entry.UpdateListing(listing);
            _calls.Add($"Listing {code}");
            return Task.CompletedTask;
        }
    }

    public Task<OpenLobbies> GetOpenLobbies()
    {
        lock (_entries)
        {
            var lobbies = new OpenLobbies();

            foreach (var (code, entry) in _entries)
            {
                if (entry.Listing.IsListed == true)
                {
                    lobbies.Entries.Add(new OpenLobby
                    {
                        Code = code,
                        ServerId = entry.ServerId,
                        Listing = entry.Listing
                    });
                }
            }

            return Task.FromResult(lobbies);
        }
    }

    // The open lobby of the code, null if it is not listed.
    public LobbyListing? FindListed(string code)
    {
        lock (_entries)
        {
            if (_entries.TryGetValue(code, out var entry) == false || entry.Listing.IsListed == false)
                return null;

            return entry.Listing;
        }
    }

    public Task<string> Register(Guid serverId, Guid sessionId)
    {
        lock (_entries)
        {
            var code = LobbyCodeGenerator.GenerateUnique(_entries.ContainsKey, _generator ?? LobbyCodeGenerator.Generate);

            _entries.Add(code, new LobbyRegistryEntry
            {
                ServerId = serverId,
                SessionId = sessionId
            });

            return Task.FromResult(code);
        }
    }

    public Task<LobbyRegistryEntry?> Find(string code)
    {
        lock (_entries)
            return Task.FromResult(_entries.GetValueOrDefault(code));
    }

    public async Task MarkStarted(string code, Guid sessionId, Guid matchSessionId)
    {
        if (MarkStartedGate != null)
            await MarkStartedGate.Task;

        lock (_entries)
        {
            if (_entries.TryGetValue(code, out var entry) == false || entry.SessionId != sessionId)
                throw new InvalidOperationException($"Lobby code {code} is not held by session {sessionId}");

            if (entry.MatchSessionId != Guid.Empty)
                throw new InvalidOperationException($"Lobby code {code} is already marked as started");

            entry.MarkStarted(matchSessionId);
            _calls.Add($"Mark {code}");
        }
    }

    public Task Release(string code, Guid sessionId)
    {
        lock (_entries)
        {
            if (_entries.TryGetValue(code, out var entry) == false || entry.SessionId != sessionId)
                throw new InvalidOperationException($"Lobby code {code} is not held by session {sessionId}");

            _entries.Remove(code);
            _calls.Add($"Release {code}");
            return Task.CompletedTask;
        }
    }
}
