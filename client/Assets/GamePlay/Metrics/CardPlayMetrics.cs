using System;
using System.Collections.Generic;
using GamePlay.Loop;
using Global.MatchMetrics;
using Internal;
using Shared;

namespace GamePlay.Metrics
{
    // The cards the local player played: cards.played {card} and the event card.play {card, board = own / enemy, cells -
    // the aimed cells when the card aims at a board}. The type of a played card is the one of its CardAdd: every card
    // comes to a hand through one.
    public class CardPlayMetrics : IScopeSetup
    {
        public CardPlayMetrics(IMatchMetricsSession session, IMatchSnapshots snapshots, IGameContext context)
        {
            _session = session;
            _snapshots = snapshots;
            _context = context;
        }

        private readonly IMatchMetricsSession _session;
        private readonly IMatchSnapshots _snapshots;
        private readonly IGameContext _context;
        private readonly Dictionary<Guid, CardType> _types = new();
        private readonly Dictionary<CardType, IMatchCounter> _played = new();

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _snapshots.Received.Advise(lifetime, OnSnapshot);
        }

        private void OnSnapshot(SharedMoveSnapshot snapshot)
        {
            foreach (var record in snapshot.Records)
            {
                switch (record)
                {
                    case PlayerSnapshotRecord.CardAdd add:
                        _types[add.CardId] = add.Type;
                        break;
                    case PlayerSnapshotRecord.CardUse use when use.PlayerId == _context.Self.Id:
                        OnPlayed(use);
                        break;
                }
            }
        }

        private void OnPlayed(PlayerSnapshotRecord.CardUse use)
        {
            if (_types.TryGetValue(use.CardId, out var type) == false)
                throw new InvalidOperationException($"[Metrics] A played card {use.CardId} that never came to a hand");

            var card = CardTagNames.Get(type);

            if (_played.TryGetValue(type, out var counter) == false)
            {
                counter = _session.Stream.Counter("cards.played", MatchTags.Of("card", card));
                _played.Add(type, counter);
            }

            counter.Add(1);

            var board = MatchEventField.Of("board", use.Data.TargetPlayer == _context.Self.Id ? "own" : "enemy");
            var cells = CardActionCells.Targeted(use.Data);

            if (cells == CardActionCells.Unknown)
                _session.Stream.Event("card.play", MatchEventField.Of("card", card), board);
            else
                _session.Stream.Event("card.play", MatchEventField.Of("card", card), board, MatchEventField.Of("cells", cells));
        }
    }
}
