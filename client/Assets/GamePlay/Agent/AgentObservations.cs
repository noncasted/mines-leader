#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Agent
{
    // Observations the server sends to the agent: the last one and the waits for a newer one.
    public class AgentObservations : IScopeSetup
    {
        private readonly List<ObservationWaiter> _waiters = new();

        public SharedAgentObservation Last { get; private set; }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            lifetime.Listen(ReleaseWaiters);
        }

        public void Receive(SharedAgentObservation observation)
        {
            Last = observation;

            for (var index = _waiters.Count - 1; index >= 0; index--)
            {
                var waiter = _waiters[index];

                if (observation.Sequence <= waiter.AfterSequence)
                    continue;

                _waiters.RemoveAt(index);
                waiter.Source.TrySetResult(observation);
            }
        }

        public async UniTask<SharedAgentObservation> Wait(int afterSequence, int timeoutMs)
        {
            if (Last != null && Last.Sequence > afterSequence)
                return Last;

            var source = new UniTaskCompletionSource<SharedAgentObservation>();
            var waiter = new ObservationWaiter(afterSequence, source);
            _waiters.Add(waiter);

            try
            {
                var (hasResultLeft, result) = await UniTask.WhenAny(source.Task, UniTask.Delay(timeoutMs));

                if (hasResultLeft)
                    return result;

                return Error("Timed out waiting for observation");
            }
            finally
            {
                _waiters.Remove(waiter);
            }
        }

        /// <summary>
        /// A server error of an action (a card without a target) comes as an observation with HasError and stays in
        /// Last. It belongs to that action only: the caller gets it once, state / wait then see the same frame without
        /// the flag. Otherwise game-agent.py state fails until the next frame and breaks board-solver.py --from-agent.
        /// </summary>
        public SharedAgentObservation ConsumeError(SharedAgentObservation observation)
        {
            if (observation.HasError == false || ReferenceEquals(observation, Last) == false)
                return observation;

            Last = new SharedAgentObservation
            {
                Sequence = observation.Sequence,
                EventCursor = observation.EventCursor,
                IsOwnTurn = observation.IsOwnTurn,
                GameOver = observation.GameOver,
                WinnerId = observation.WinnerId,
                WinReason = observation.WinReason,
                Trigger = observation.Trigger,
                HasError = false,
                Error = string.Empty,
                Events = observation.Events,
                Self = observation.Self,
                Opponent = observation.Opponent
            };

            return observation;
        }

        // An error of the client side, on top of the last frame so the caller still sees the boards.
        public SharedAgentObservation Error(string error)
        {
            return new SharedAgentObservation
            {
                HasError = true,
                Error = error,
                Sequence = Last?.Sequence ?? 0,
                EventCursor = Last?.EventCursor ?? 0,
                IsOwnTurn = Last?.IsOwnTurn ?? false,
                GameOver = Last?.GameOver ?? false,
                WinnerId = Last?.WinnerId ?? default,
                WinReason = Last?.WinReason ?? string.Empty,
                Trigger = Last?.Trigger ?? string.Empty,
                Events = new List<string>(),
                Self = Last?.Self ?? new AgentPlayerView(),
                Opponent = Last?.Opponent ?? new AgentPlayerView()
            };
        }

        private void ReleaseWaiters()
        {
            // A released wait removes itself from the list in its finally: release a copy.
            var waiters = _waiters.ToArray();
            _waiters.Clear();

            foreach (var waiter in waiters)
                waiter.Source.TrySetResult(Error("Agent match ended"));
        }

        private sealed class ObservationWaiter
        {
            public ObservationWaiter(int afterSequence, UniTaskCompletionSource<SharedAgentObservation> source)
            {
                AfterSequence = afterSequence;
                Source = source;
            }

            public int AfterSequence { get; }
            public UniTaskCompletionSource<SharedAgentObservation> Source { get; }
        }
    }
}
#endif
