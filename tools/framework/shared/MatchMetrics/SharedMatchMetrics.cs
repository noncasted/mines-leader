using System;
using MemoryPack;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shared
{
    // The game data of a match over the meta socket of a client (module match-metrics): series of numbers in buckets of
    // one second of the clock of the run and rare events. A stream is one writer in one match: the server knows the user
    // from the socket, the client names the match and its writer. Union tags: NetworkContextTags.MatchMetrics* (the
    // project lists them in its union). One batch in flight per stream; a batch not answered goes again with the same Seq
    // after a new Open of the same writer.
    public static partial class SharedMatchMetrics
    {
        // Opens the stream of (match, user of the socket, writer) or finds it again after a reconnect. Answered by Opened.
        [MemoryPackable]
        public partial class Open : IUnprofiledRequest
        {
            public Guid MatchId { get; set; }

            // Of the client, one per scope of a run: a run entered again is a new stream of the same user.
            public Guid WriterId { get; set; }
            public MatchStreamKind Kind { get; set; }
        }

        // StreamId and LastSeq are valid with Ok only. LastSeq: the last batch the server has, 0 none; the client sends from
        // LastSeq + 1.
        [MemoryPackable]
        public partial class Opened : INetworkContext
        {
            public MatchMetricsStatus Status { get; set; }
            public Guid StreamId { get; set; }
            public int LastSeq { get; set; }
        }

        // Seq grows by 1 from 1 in a stream. Series: the series first used in this batch, each in its slot; a chunk names its
        // series by the slot. Answered by BatchResult.
        [MemoryPackable]
        public partial class Batch : IUnprofiledRequest
        {
            public Guid StreamId { get; set; }
            public int Seq { get; set; }
            public MatchSeriesDecl[] Series { get; set; }
            public MatchChunk[] Chunks { get; set; }
            public MatchEvent[] Events { get; set; }
        }

        // Ok and Duplicate: the batch is stored (Duplicate - by an earlier copy), the client sends the next one. Rejected: the
        // batch breaks the rules (a gap in Seq, the limits). Closed: the match takes no more data, the client stops.
        [MemoryPackable]
        public partial class BatchResult : INetworkContext
        {
            public MatchMetricsStatus Status { get; set; }
        }

        // Tags: up to 4 pairs, Keys[i] = Values[i].
        [MemoryPackable]
        public partial class MatchSeriesDecl
        {
            public int Slot { get; set; }
            public string Name { get; set; }
            public MatchSeriesKind Kind { get; set; }
            public string[] TagKeys { get; set; }
            public string[] TagValues { get; set; }
        }

        // The values of one series in the buckets Start, Start + 1, ...: a counter - the sum of a bucket, a gauge - the last
        // value of a bucket, NaN unchanged. AgeMs: the real time since the first bucket began, at the send; the server puts the
        // chunk at its receive time minus AgeMs, so the real time of a chunk runs with the buckets (a chunk ends where the
        // clock of the run stops or jumps).
        [MemoryPackable]
        public partial class MatchChunk
        {
            public int Slot { get; set; }
            public int Start { get; set; }
            public long AgeMs { get; set; }
            public float[] Values { get; set; }
        }

        // Time: seconds of the clock of the run; AgeMs as in MatchChunk.
        [MemoryPackable]
        public partial class MatchEvent
        {
            public string Name { get; set; }
            public float Time { get; set; }
            public long AgeMs { get; set; }
            public MatchEventField[] Fields { get; set; }
        }

        // A number or a short text (Text null: a number).
        [MemoryPackable]
        public partial class MatchEventField
        {
            public string Key { get; set; }
            public double Number { get; set; }
            public string Text { get; set; }
        }

        public enum MatchStreamKind : byte
        {
            Client = 0,
            Server = 1
        }

        public enum MatchSeriesKind : byte
        {
            Counter = 0,
            Gauge = 1
        }

        // NotInMatch: the user is not a player of the match (Open only).
        public enum MatchMetricsStatus : byte
        {
            Ok = 0,
            Duplicate = 1,
            Rejected = 2,
            Closed = 3,
            NotInMatch = 4
        }
    }
}
