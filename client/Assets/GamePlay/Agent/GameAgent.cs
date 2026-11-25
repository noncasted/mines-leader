#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Cysharp.Threading.Tasks;
using GamePlay.Cards;
using GamePlay.Loop;
using Internal;
using Shared;

namespace GamePlay.Agent
{
    // The agent of a match: sends the requests of a human client and answers with the next observation.
    public class GameAgent : IGameAgent, IScopeSetup
    {
        public GameAgent(
            INetworkConnection connection,
            IGameRound gameRound,
            IGameState gameState,
            AgentObservations observations,
            AgentHandCards handCards,
            AgentBoardView boardView,
            ILog<GameAgent> log)
        {
            _connection = connection;
            _gameRound = gameRound;
            _gameState = gameState;
            _observations = observations;
            _handCards = handCards;
            _boardView = boardView;
            _log = log;
        }

        private const int ActionTimeoutMs = 10000;

        private readonly INetworkConnection _connection;
        private readonly IGameRound _gameRound;
        private readonly IGameState _gameState;
        private readonly AgentObservations _observations;
        private readonly AgentHandCards _handCards;
        private readonly AgentBoardView _boardView;
        private readonly ILog<GameAgent> _log;

        public SharedAgentObservation LastObservation => _observations.Last;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            GameAgentAccess.Attach(lifetime, this);
        }

        public SharedAgentObservation State()
        {
            return _observations.Last ?? _observations.Error("No observation yet");
        }

        public UniTask<SharedAgentObservation> Open(int x, int y)
        {
            return SendAction(true, new SharedGameAction.Open { Position = new Position(x, y) });
        }

        public UniTask<SharedAgentObservation> Chord(int x, int y)
        {
            return SendAction(true, new SharedGameAction.OpenMultiple { Position = new Position(x, y) });
        }

        // Flags are legal during the opponent turn.
        public UniTask<SharedAgentObservation> Flag(int x, int y)
        {
            return SendAction(false, new SharedGameAction.SetFlag { Position = new Position(x, y) });
        }

        public UniTask<SharedAgentObservation> Unflag(int x, int y)
        {
            return SendAction(false, new SharedGameAction.RemoveFlag { Position = new Position(x, y) });
        }

        public async UniTask<SharedAgentObservation> UseCard(
            Guid? cardId,
            string typeName,
            int? x,
            int? y,
            Guid? extraCardId,
            int? chosenIndex)
        {
            if (ShouldRefuseOffTurn())
                return _observations.Error("Not your turn");

            Guid id;

            if (cardId.HasValue && cardId.Value != Guid.Empty)
                id = cardId.Value;
            else if (string.IsNullOrWhiteSpace(typeName))
                return _observations.Error("card_id or type required");
            else if (Enum.TryParse(typeName, out CardType parsed) == false)
                return _observations.Error("Invalid type");
            else if (_handCards.TryFindByType(parsed, out id) == false)
                return _observations.Error("Card type not in hand");

            if (_handCards.TryGetType(id, out var type) == false)
                return _observations.Error("Card not found");

            Position? position = null;

            if (x.HasValue && y.HasValue)
                position = new Position(x.Value, y.Value);

            var extra = extraCardId;

            if (type == CardType.ZipZap || type == CardType.ZipZap_Max)
                extra = extraCardId ?? id;

            ICardUsePayload payload;

            try
            {
                payload = CardUsePayloadFactory.Create(type, position, extra, chosenIndex);
            }
            catch (ArgumentException exception)
            {
                return _observations.Error(exception.Message);
            }

            return await SendAction(false, new SharedGameAction.CardUse { CardId = id, Payload = payload });
        }

        public UniTask<SharedAgentLegalPlaysResponse> LegalPlays()
        {
            return _connection.Request<SharedAgentLegalPlaysResponse>(new SharedAgentLegalPlaysRequest());
        }

        public async UniTask<SharedAgentObservation> EndTurn(int timeoutMs)
        {
            if (ShouldRefuseOffTurn())
                return _observations.Error("Not your turn");

            var afterSequence = _observations.Last?.Sequence ?? -1;
            var sendError = await SendRequest(new SharedGameAction.SkipTurn());

            if (sendError != null)
                return sendError;

            return await WaitUntilOwnTurnOrOver(afterSequence, timeoutMs, true);
        }

        public async UniTask<SharedAgentObservation> WaitOwnTurn(int timeoutMs)
        {
            var last = _observations.Last;

            if (last != null && (last.IsOwnTurn || last.GameOver))
                return last;

            return await WaitUntilOwnTurnOrOver(last?.Sequence ?? -1, timeoutMs, false);
        }

        public async UniTask<SharedAgentObservation> RequestOracleState()
        {
            var afterSequence = _observations.Last?.Sequence ?? -1;
            var sendError = await SendRequest(new SharedAgentObservationRequest());

            if (sendError != null)
                return sendError;

            return await _observations.Wait(afterSequence, ActionTimeoutMs);
        }

        public async UniTask<SharedAgentObservation> WaitVisual(int timeoutMs)
        {
            if (await _boardView.WaitIdle(timeoutMs) == false)
                return _observations.Error("Timed out waiting for visuals");

            return _observations.Last ?? new SharedAgentObservation();
        }

        public CellInspect InspectCell(int x, int y, bool opponent)
        {
            return _boardView.Inspect(x, y, opponent);
        }

        private async UniTask<SharedAgentObservation> SendAction(bool refuseOffTurn, INetworkContext request)
        {
            if (refuseOffTurn && ShouldRefuseOffTurn())
                return _observations.Error("Not your turn");

            var afterSequence = _observations.Last?.Sequence ?? -1;
            var sendError = await SendRequest(request);

            if (sendError != null)
                return sendError;

            return _observations.ConsumeError(await _observations.Wait(afterSequence, ActionTimeoutMs));
        }

        // A refused request (rules of the match, cheats off) is an answer for the agent, not a failure of the tool.
        private async UniTask<SharedAgentObservation> SendRequest(INetworkContext request)
        {
            try
            {
                await _connection.Request(request);
                return null;
            }
            catch (Exception exception)
            {
                _log.Error(exception, "Agent request {Request} failed", request.GetType().Name);
                return _observations.Error(exception.Message);
            }
        }

        private async UniTask<SharedAgentObservation> WaitUntilOwnTurnOrOver(
            int afterSequence,
            int timeoutMs,
            bool ignoreSkipAction)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (true)
            {
                var remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;

                if (remaining <= 0)
                    return _observations.Error("Timed out waiting for observation");

                var observation = await _observations.Wait(afterSequence, remaining);

                if (observation.HasError || observation.GameOver)
                    return observation;

                afterSequence = observation.Sequence;

                var skipEcho = ignoreSkipAction && string.Equals(observation.Trigger, "action", StringComparison.Ordinal);

                if (observation.IsOwnTurn && skipEcho == false)
                    return observation;
            }
        }

        private bool ShouldRefuseOffTurn()
        {
            if (IsMatchOver())
                return false;

            return _gameRound.IsTurnAllowed == false;
        }

        private bool IsMatchOver()
        {
            return _observations.Last?.GameOver == true || _gameState.Value.Value == GameStateType.Completed;
        }
    }
}
#endif
