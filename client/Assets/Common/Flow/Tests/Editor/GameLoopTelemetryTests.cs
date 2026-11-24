using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Flow.Loop;
using GamePlay.Loop;
using Global.Cameras;
using Global.Telemetry;
using Global.UI;
using Internal;
using Menu.Common;
using Meta;
using NUnit.Framework;
using Shared;
using UnityEngine;

namespace Flow.Tests
{
    // Every part of the game loop writes into its own session of the telemetry, opened before its scope is loaded; the first
    // menu session waits for the authorization of the meta socket.
    public class GameLoopTelemetryTests
    {
        private readonly List<string> _events = new();

        [SetUp]
        public void SetUp()
        {
            _events.Clear();
        }

        [Test]
        public void TheFirstMenuSessionOpensOnTheAuthorization()
        {
            var meta = new FakeMetaState();

            CreateLoop(meta, new FakeMenuLoader(_events, playFirst: false)).Start(new Lifetime());

            Assert.That(_events, Is.EqualTo(new[] { "load menu" }));

            meta.SetAuthorized();

            Assert.That(_events, Is.EqualTo(new[] { "load menu", "session menu" }));
        }

        [Test]
        public void EveryPartOpensItsSessionBeforeItsLoad()
        {
            var meta = new FakeMetaState();
            meta.SetAuthorized();

            CreateLoop(meta, new FakeMenuLoader(_events, playFirst: true)).Start(new Lifetime());

            Assert.That(_events, Is.EqualTo(new[]
            {
                "session menu", "load menu",
                "session match", "load match",
                // The rematch.
                "session match", "load match",
                "session menu", "load menu"
            }));
        }

        private GameLoop CreateLoop(IMetaState meta, IMenuLoader menuLoader)
        {
            return new GameLoop(
                menuLoader,
                new FakeGamePlayLoader(_events),
                new FakeLoadingScreen(),
                new FakeGlobalCamera(),
                meta,
                new FakeTelemetry(_events));
        }

        private class FakeTelemetry : IClientTelemetry
        {
            public FakeTelemetry(List<string> events)
            {
                _events = events;
            }

            private readonly List<string> _events;

            public void StartSession(IReadOnlyLifetime lifetime, params string[] tags)
            {
                _events.Add("session " + string.Join(",", tags));
            }

            public void Mark(string name) => throw new InvalidOperationException();
            public void AddCollector(IReadOnlyLifetime lifetime, ITelemetryCollector collector) => throw new InvalidOperationException();
            public void SendStartupTrace() => throw new InvalidOperationException();
        }

        // The first load chooses a match when asked to, every other one stays open: the loop ends there.
        private class FakeMenuLoader : IMenuLoader
        {
            public FakeMenuLoader(List<string> events, bool playFirst)
            {
                _events = events;
                _playFirst = playFirst;
            }

            private readonly List<string> _events;
            private readonly bool _playFirst;
            private int _loads;

            public UniTask<GameLoadData> Load()
            {
                _events.Add("load menu");
                _loads++;

                if (_loads == 1 && _playFirst == true)
                {
                    var result = new SharedMatchmaking.MatchResult { Type = GameMatchType.LastManStandingTurnBased };
                    return UniTask.FromResult(new GameLoadData { Result = result });
                }

                return new UniTaskCompletionSource<GameLoadData>().Task;
            }
        }

        // The first match ends with a rematch, the next one with the exit to the menu.
        private class FakeGamePlayLoader : IGamePlayLoader
        {
            public FakeGamePlayLoader(List<string> events)
            {
                _events = events;
            }

            private readonly List<string> _events;
            private int _loads;

            public UniTask<IGameEndTransition> Load(GameLoadData gameLoadData)
            {
                _events.Add("load match");
                _loads++;

                if (_loads == 1)
                {
                    var rematch = new GameEndTransition.Rematch { NewSession = new SessionData("url", Guid.NewGuid(), "ticket") };
                    return UniTask.FromResult<IGameEndTransition>(rematch);
                }

                return UniTask.FromResult<IGameEndTransition>(new GameEndTransition.Exit());
            }
        }

        private class FakeLoadingScreen : ILoadingScreen
        {
            public UniTask Show() => UniTask.CompletedTask;
            public void ShowInstantly() => throw new InvalidOperationException();
            public void Hide() => throw new InvalidOperationException();
            public UniTask WaitHidden(IReadOnlyLifetime lifetime) => throw new InvalidOperationException();
        }

        private class FakeGlobalCamera : IGlobalCamera
        {
            public Camera Camera => throw new InvalidOperationException();

            public void Enable()
            {
            }

            public void Disable() => throw new InvalidOperationException();
        }

        private class FakeMetaState : IMetaState
        {
            private readonly ViewableProperty<bool> _isAuthorized = new(false);

            public IViewableProperty<bool> IsAuthorized => _isAuthorized;
            public IViewableProperty<bool> IsProjectionsReceived => throw new InvalidOperationException();
            public IViewableProperty<bool> IsRegistriesLoaded => throw new InvalidOperationException();
            public IViewableProperty<bool> IsReady => throw new InvalidOperationException();

            public void SetAuthorized() => _isAuthorized.Set(true);
            public void SetRegistriesLoaded() => throw new InvalidOperationException();
        }
    }
}
