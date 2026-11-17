using System;
using GamePlay.Loop;
using Global.MatchMetrics;
using Internal;
using Shared;

namespace GamePlay.Metrics
{
    // The turns of the local player: turns (started) and the event turn.end {number - the turn of this player from 1,
    // seconds - its length on the clock of the match, cells - the cells opened on its board, cards - the cards played}.
    // A turn starts with the round record that gives the move to this player and ends with the record of the other player,
    // the completion of the match or the end of the stream. The players alternate; the round number is not a turn: the
    // server raises it at the end of a turn with the same player in the record (LastManStandingRound).
    public class TurnMetrics : IScopeSetup
    {
        public TurnMetrics(
            IMatchMetricsSession session,
            IMatchSnapshots snapshots,
            IGameContext context,
            MatchClock clock)
        {
            _session = session;
            _snapshots = snapshots;
            _context = context;
            _clock = clock;
        }

        private readonly IMatchMetricsSession _session;
        private readonly IMatchSnapshots _snapshots;
        private readonly IGameContext _context;
        private readonly MatchClock _clock;

        private IMatchCounter _turns;
        private Guid _roundPlayer;
        private bool _isTurn;
        private int _number;
        private float _startedAt;
        private int _cells;
        private int _cards;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _turns = _session.Stream.Counter("turns");
            _snapshots.Received.Advise(lifetime, OnSnapshot);
            _session.Ending.Advise(lifetime, EndTurn);
        }

        private void OnSnapshot(SharedMoveSnapshot snapshot)
        {
            foreach (var record in snapshot.Records)
            {
                switch (record)
                {
                    case LastManStandingRoundRecord round:
                        OnRound(round.CurrentPlayer);
                        break;
                    case TimeLimitedRoundRecord round:
                        OnRound(round.CurrentPlayer);
                        break;
                    case SharedBoardSnapshot board when _isTurn == true && board.BoardOwnerId == _context.Self.Id:
                        _cells += BoardOpening.Of(board).Total;
                        break;
                    case PlayerSnapshotRecord.CardUse use when _isTurn == true && use.PlayerId == _context.Self.Id:
                        _cards++;
                        _cells += CardActionCells.Opened(use.Data, _context.Self.Id);
                        break;
                    case GameCompletedRecord:
                        EndTurn();
                        break;
                }
            }
        }

        private void OnRound(Guid player)
        {
            if (player == _roundPlayer)
                return;

            _roundPlayer = player;

            EndTurn();

            if (player == _context.Self.Id)
                StartTurn();
        }

        private void StartTurn()
        {
            _isTurn = true;
            _number++;
            _startedAt = _clock.Seconds;
            _cells = 0;
            _cards = 0;
            _turns.Add(1);
        }

        private void EndTurn()
        {
            if (_isTurn == false)
                return;

            _isTurn = false;

            _session.Stream.Event("turn.end",
                MatchEventField.Of("number", _number),
                MatchEventField.Of("seconds", _clock.Seconds - _startedAt),
                MatchEventField.Of("cells", _cells),
                MatchEventField.Of("cards", _cards));
        }
    }
}
