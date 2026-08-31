using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Boards;
using GamePlay.Cards;
using GamePlay.Loop;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Agent {
    public static class GameAgentBridge {
        public static bool IsActive { get; private set; }
        public static SharedAgentObservation LastObservation { get; private set; }

        private static readonly List<ObservationWaiter> _waiters = new();
        private static INetworkConnection _connection;
        private static IGameContext _gameContext;
        private static IGameRound _gameRound;
        private static IGameState _gameState;

        public static void Set(
            INetworkConnection connection,
            IGameContext gameContext,
            IGameRound gameRound,
            IGameState gameState) {
            _connection = connection;
            _gameContext = gameContext;
            _gameRound = gameRound;
            _gameState = gameState;
            IsActive = true;
        }

        public static void Clear() {
            _connection = null;
            _gameContext = null;
            _gameRound = null;
            _gameState = null;
            LastObservation = null;
            IsActive = false;

            foreach (var waiter in _waiters)
                waiter.Source.TrySetResult(ErrorObservation("Agent bridge cleared"));

            _waiters.Clear();
        }

        public static void OnObservation(SharedAgentObservation observation) {
            if (observation == null)
                return;

            LastObservation = observation;

            for (var index = _waiters.Count - 1; index >= 0; index--) {
                var waiter = _waiters[index];
                if (observation.Sequence <= waiter.AfterSequence)
                    continue;

                _waiters.RemoveAt(index);
                waiter.Source.TrySetResult(observation);
            }
        }

        public static async UniTask<SharedAgentObservation> WaitObservation(int afterSequence, int timeoutMs) {
            if (timeoutMs <= 0)
                timeoutMs = 10000;

            if (LastObservation != null && LastObservation.Sequence > afterSequence)
                return LastObservation;

            var source = new UniTaskCompletionSource<SharedAgentObservation>();
            var waiter = new ObservationWaiter(afterSequence, source);
            _waiters.Add(waiter);

            try {
                var (hasResultLeft, result) = await UniTask.WhenAny(source.Task, UniTask.Delay(timeoutMs));
                if (hasResultLeft)
                    return result;

                return ErrorObservation("Timed out waiting for observation");
            }
            finally {
                _waiters.Remove(waiter);
            }
        }

        public static UniTask<SharedAgentObservation> Open(int x, int y) {
            return SendAction(
                refuseOffTurn: true,
                () => new SharedGameAction.Open { Position = new Position(x, y) });
        }

        public static UniTask<SharedAgentObservation> Chord(int x, int y) {
            return SendAction(
                refuseOffTurn: true,
                () => new SharedGameAction.OpenMultiple { Position = new Position(x, y) });
        }

        public static UniTask<SharedAgentObservation> Flag(int x, int y) {
            return SendAction(
                refuseOffTurn: false,
                () => new SharedGameAction.SetFlag { Position = new Position(x, y) });
        }

        public static UniTask<SharedAgentObservation> Unflag(int x, int y) {
            return SendAction(
                refuseOffTurn: false,
                () => new SharedGameAction.RemoveFlag { Position = new Position(x, y) });
        }

        public static UniTask<SharedAgentObservation> UseCard(
            Guid cardId,
            int? x,
            int? y,
            Guid? extraCardId,
            int? chosenIndex) {
            return UseCard(cardId, typeName: null, x, y, extraCardId, chosenIndex);
        }

        public static async UniTask<SharedAgentObservation> UseCard(
            Guid? cardId,
            string typeName,
            int? x,
            int? y,
            Guid? extraCardId,
            int? chosenIndex) {
            if (IsActive == false || _connection == null)
                return ErrorObservation("Agent bridge is not active");

            if (ShouldRefuseOffTurn())
                return ErrorObservation("Not your turn");

            if (cardId.HasValue == false || cardId.Value == Guid.Empty) {
                var resolved = ResolveCardIdByType(typeName);
                if (resolved.HasError)
                    return resolved.Error;

                cardId = resolved.CardId;
            }

            if (TryGetCardType(cardId.Value, out var type) == false)
                return ErrorObservation("Card not found");

            Position? position = null;
            if (x.HasValue && y.HasValue)
                position = new Position(x.Value, y.Value);

            var extra = extraCardId;
            if (type == CardType.ZipZap || type == CardType.ZipZap_Max)
                extra = extraCardId ?? cardId;

            ICardUsePayload payload;
            try {
                payload = CardUsePayloadFactory.Create(type, position, extra, chosenIndex);
            }
            catch (ArgumentException exception) {
                return ErrorObservation(exception.Message);
            }

            return await SendAction(
                refuseOffTurn: false,
                () => new SharedGameAction.CardUse {
                    CardId = cardId.Value,
                    Payload = payload
                });
        }

        public static async UniTask<SharedAgentLegalPlaysResponse> LegalPlays() {
            if (IsActive == false || _connection == null) {
                return new SharedAgentLegalPlaysResponse {
                    Cards = new List<AgentLegalCardView>()
                };
            }

            try {
                return await _connection.Request<SharedAgentLegalPlaysResponse>(
                    new SharedAgentLegalPlaysRequest());
            }
            catch (Exception exception) {
                Debug.LogError($"[GameAgent] Legal plays failed: {exception}");
                return new SharedAgentLegalPlaysResponse {
                    Cards = new List<AgentLegalCardView>()
                };
            }
        }

        public static CellInspect InspectCell(int x, int y, bool opponent) {
            var missing = new CellInspect {
                X = x,
                Y = y,
                Exists = false,
                State = string.Empty,
                Effects = new List<string>()
            };

            if (IsActive == false || _gameContext == null)
                return missing;

            var player = opponent ? _gameContext.Other : _gameContext.Self;
            if (player?.Board == null)
                return missing;

            if (player.Board.Cells.TryGetValue(new Vector2Int(x, y), out var cell) == false)
                return missing;

            if (cell is CellView view)
                return view.Inspect();

            return missing;
        }

        public static async UniTask<SharedAgentObservation> WaitVisual(int timeoutMs) {
            if (timeoutMs <= 0)
                timeoutMs = 5000;

            if (IsActive == false)
                return ErrorObservation("Agent bridge is not active");

            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (DateTime.UtcNow < deadline) {
                if (AnyOwnCellAnimating() == false)
                    return LastObservation ?? new SharedAgentObservation();

                await UniTask.Delay(50);
            }

            return ErrorObservation("Timed out waiting for visuals");
        }

        public static async UniTask<SharedAgentObservation> EndTurn(int timeoutMs) {
            if (timeoutMs <= 0)
                timeoutMs = 60000;

            if (IsActive == false || _connection == null)
                return ErrorObservation("Agent bridge is not active");

            if (ShouldRefuseOffTurn())
                return ErrorObservation("Not your turn");

            var afterSequence = LastObservation?.Sequence ?? -1;
            var sendError = await SendRequest(new SharedGameAction.SkipTurn());
            if (sendError != null)
                return sendError;

            return await WaitUntilOwnTurnOrOver(afterSequence, timeoutMs, ignoreSkipAction: true);
        }

        public static SharedAgentObservation GetState(bool oracle) {
            if (oracle == false)
                return LastObservation ?? ErrorObservation("No observation yet");

            return RequestOracleState().GetAwaiter().GetResult();
        }

        public static async UniTask<SharedAgentObservation> RequestOracleState() {
            if (IsActive == false || _connection == null)
                return ErrorObservation("Agent bridge is not active");

            var afterSequence = LastObservation?.Sequence ?? -1;
            var sendError = await SendRequest(new SharedAgentObservationRequest());
            if (sendError != null)
                return sendError;

            return await WaitObservation(afterSequence, 10000);
        }

        public static async UniTask<SharedAgentObservation> WaitOwnTurn(int timeoutMs) {
            if (timeoutMs <= 0)
                timeoutMs = 60000;

            if (LastObservation != null && (LastObservation.IsOwnTurn || LastObservation.GameOver))
                return LastObservation;

            return await WaitUntilOwnTurnOrOver(LastObservation?.Sequence ?? -1, timeoutMs, ignoreSkipAction: false);
        }

        private static async UniTask<SharedAgentObservation> SendAction(
            bool refuseOffTurn,
            Func<INetworkContext> createRequest) {
            if (refuseOffTurn && ShouldRefuseOffTurn())
                return ErrorObservation("Not your turn");

            var afterSequence = LastObservation?.Sequence ?? -1;
            var sendError = await SendRequest(createRequest());
            if (sendError != null)
                return sendError;

            return await WaitObservation(afterSequence, 10000);
        }

        private static async UniTask<SharedAgentObservation> SendRequest(INetworkContext request) {
            if (IsActive == false || _connection == null)
                return ErrorObservation("Agent bridge is not active");

            try {
                await _connection.Request(request);
                return null;
            }
            catch (Exception exception) {
                Debug.LogError($"[GameAgent] Request failed: {exception}");
                return ErrorObservation(exception.Message);
            }
        }

        private static async UniTask<SharedAgentObservation> WaitUntilOwnTurnOrOver(
            int afterSequence,
            int timeoutMs,
            bool ignoreSkipAction) {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (true) {
                var remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                if (remaining <= 0)
                    return ErrorObservation("Timed out waiting for observation");

                var observation = await WaitObservation(afterSequence, remaining);
                if (observation.HasError)
                    return observation;

                afterSequence = observation.Sequence;

                if (observation.GameOver)
                    return observation;

                var skipEcho = ignoreSkipAction &&
                               string.Equals(observation.Trigger, "action", StringComparison.Ordinal);
                if (observation.IsOwnTurn && skipEcho == false)
                    return observation;
            }
        }

        private static bool ShouldRefuseOffTurn() {
            if (IsMatchOver())
                return false;

            if (_gameRound == null || _gameContext?.Self == null)
                return true;

            return _gameRound.IsTurnAllowed == false;
        }

        private static bool IsMatchOver() {
            if (LastObservation != null && LastObservation.GameOver)
                return true;

            return _gameState != null && _gameState.Value.Value == GameStateType.Completed;
        }

        private static (bool HasError, SharedAgentObservation Error, Guid CardId) ResolveCardIdByType(string typeName) {
            if (string.IsNullOrWhiteSpace(typeName))
                return (true, ErrorObservation("card_id or type required"), Guid.Empty);

            if (Enum.TryParse(typeName, out CardType parsed) == false)
                return (true, ErrorObservation("Invalid type"), Guid.Empty);

            var hand = _gameContext?.Self?.Hand;
            if (hand != null) {
                foreach (ICard card in hand.Entries) {
                    if (card.Type != parsed)
                        continue;

                    return (false, null, card.Id);
                }
            }

            var cards = LastObservation?.Self?.Hand;
            if (cards != null) {
                foreach (var card in cards) {
                    if (string.Equals(card.Type, parsed.ToString(), StringComparison.Ordinal) == false)
                        continue;

                    return (false, null, card.Id);
                }
            }

            return (true, ErrorObservation("Card type not in hand"), Guid.Empty);
        }

        private static bool AnyOwnCellAnimating() {
            var board = _gameContext?.Self?.Board;
            if (board?.Cells == null)
                return false;

            foreach (var cell in board.Cells.Values) {
                if (cell is CellView view && view.IsAnimating)
                    return true;
            }

            return false;
        }

        private static bool TryGetCardType(Guid cardId, out CardType type) {
            type = default;

            var hand = _gameContext?.Self?.Hand;
            if (hand != null) {
                foreach (ICard card in hand.Entries) {
                    if (card.Id != cardId)
                        continue;

                    type = card.Type;
                    return true;
                }
            }

            var cards = LastObservation?.Self?.Hand;
            if (cards == null)
                return false;

            foreach (var card in cards) {
                if (card.Id != cardId)
                    continue;

                if (Enum.TryParse(card.Type, out type) == false)
                    return false;

                return true;
            }

            return false;
        }

        private static SharedAgentObservation ErrorObservation(string error) {
            return new SharedAgentObservation {
                HasError = true,
                Error = error ?? string.Empty,
                Sequence = LastObservation?.Sequence ?? 0,
                EventCursor = LastObservation?.EventCursor ?? 0,
                IsOwnTurn = LastObservation?.IsOwnTurn ?? false,
                GameOver = LastObservation?.GameOver ?? false,
                WinnerId = LastObservation?.WinnerId ?? Guid.Empty,
                WinReason = LastObservation?.WinReason ?? string.Empty,
                Trigger = LastObservation?.Trigger ?? string.Empty,
                Events = new List<string>(),
                Self = LastObservation?.Self ?? new AgentPlayerView(),
                Opponent = LastObservation?.Opponent ?? new AgentPlayerView()
            };
        }

        private sealed class ObservationWaiter {
            public ObservationWaiter(int afterSequence, UniTaskCompletionSource<SharedAgentObservation> source) {
                AfterSequence = afterSequence;
                Source = source;
            }

            public int AfterSequence { get; }
            public UniTaskCompletionSource<SharedAgentObservation> Source { get; }
        }
    }

    public class GameAgentService : IScopeSetup {
        public GameAgentService(
            INetworkConnection connection,
            IGameContext gameContext,
            IGameRound gameRound,
            IGameState gameState) {
            _connection = connection;
            _gameContext = gameContext;
            _gameRound = gameRound;
            _gameState = gameState;
        }

        private readonly INetworkConnection _connection;
        private readonly IGameContext _gameContext;
        private readonly IGameRound _gameRound;
        private readonly IGameState _gameState;

        public void OnSetup(IReadOnlyLifetime lifetime) {
            GameAgentBridge.Set(_connection, _gameContext, _gameRound, _gameState);
            lifetime.Listen(GameAgentBridge.Clear);
        }
    }
}
