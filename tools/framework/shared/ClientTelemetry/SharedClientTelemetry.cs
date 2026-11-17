using System;
using MemoryPack;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shared
{
    // The telemetry of a client over its meta socket. A session is a part of the game the game names with its tags (a menu, a
    // match); at most one is open on a socket, nothing is sent outside it but the startup trace. Union tags: NetworkContextTags.Telemetry* (the
    // project lists them in its union). backend/ClientTelemetry/Contract/CONTRACT.md has the rules.
    // The requests are IUnprofiledRequest: the telemetry reports the startup trace and does not write itself into it.
    public static partial class SharedClientTelemetry
    {
        // Opens a session and closes the one still open on this socket. Answered by SessionStarted.
        [MemoryPackable]
        public partial class SessionStart : IUnprofiledRequest
        {
            public Device Device { get; set; }
            public string[] Tags { get; set; }

            // The startup trace of this client, as StartupTraceResult gave it on this socket. Guid.Empty: none.
            public Guid StartupId { get; set; }

            // The session of this browser before this one that never closed: the page was killed, reloaded or frozen inside it.
            // Null: none, or the previous one closed.
            public PreviousSession Previous { get; set; }
        }

        // What a client kept of its session that did not close, from its own storage: the server may have got less of it. Time and
        // LastSampleTime are of that session; SinceMs is the time between its last record and the start that reports it, -1 unknown.
        [MemoryPackable]
        public partial class PreviousSession
        {
            public Guid SessionId { get; set; }
            public string[] Tags { get; set; }
            public long LastSampleTime { get; set; }
            public long SinceMs { get; set; }
            public Mark[] Marks { get; set; }

            // The last samples it took, oldest first, with the main numbers of a frame only: Sample.Values by the slots of SampleNames,
            // NaN no value. Null lists: none (a trace written before them).
            public string[] SampleNames { get; set; }
            public Sample[] Samples { get; set; }
        }

        // Enabled false: nothing is open, the client sends nothing until its next SessionStart. SessionId comes from the server:
        // a client cannot address a session of another one.
        [MemoryPackable]
        public partial class SessionStarted : INetworkContext
        {
            public bool Enabled { get; set; }
            public Guid SessionId { get; set; }
        }

        // One batch in flight at a time: the next one goes after the answer. Answered by BatchResult.
        [MemoryPackable]
        public partial class Batch : IUnprofiledRequest
        {
            public Guid SessionId { get; set; }

            // The new names of the session: each takes the next slot of its list, Sample.Values goes by the slots.
            public MetricName[] Names { get; set; }
            public Sample[] Samples { get; set; }
            public Error[] Errors { get; set; }
            public Mark[] Marks { get; set; }
        }

        // Enabled false: this session goes on no more (the flag is off, or it is not the session open on this socket), the
        // client sends nothing until its next SessionStart.
        [MemoryPackable]
        public partial class BatchResult : INetworkContext
        {
            public bool Enabled { get; set; }
        }

        // The session is over, closed with status ended. Answered by EmptyResponse.
        [MemoryPackable]
        public partial class SessionEnd : IUnprofiledRequest
        {
            public Guid SessionId { get; set; }
        }

        // The trace of the start of a client: the only message that needs no open session. A snapshot of an open trace, or the
        // final one, which replaces the snapshot of the same StartupId. Guid.Empty asks for a new record. Answered by StartupTraceResult.
        [MemoryPackable]
        public partial class StartupTrace : IUnprofiledRequest
        {
            public Guid StartupId { get; set; }
            public Device Device { get; set; }
            public string Name { get; set; }
            public bool Final { get; set; }
            public double DurationMs { get; set; }
            public int Frames { get; set; }
            public StartupSpan[] Spans { get; set; }
        }

        // Enabled false: nothing is written and the client sends no more traces on this socket: the flag is off, StartupId was not
        // given on this socket, or the socket is over its limits. StartupId comes from the server and lives as long as the socket.
        [MemoryPackable]
        public partial class StartupTraceResult : INetworkContext
        {
            public bool Enabled { get; set; }
            public Guid StartupId { get; set; }
        }

        // ParentIndex: the index of the parent in StartupTrace.Spans, below the index of this span; -1 for a root. Times: milliseconds
        // since the start of the trace. AllocBytes: 0 where the platform does not measure them. File: the name of a file the page loaded
        // before the build started (no host, path or query), null for any other span; TransferBytes: what came over the network, 0
        // unknown; Cached: it came from the cache of the browser.
        [MemoryPackable]
        public partial class StartupSpan
        {
            public int ParentIndex { get; set; }
            public string Name { get; set; }
            public double StartMs { get; set; }
            public double DurationMs { get; set; }
            public int Frames { get; set; }
            public bool Unfinished { get; set; }
            public long AllocBytes { get; set; }
            public string File { get; set; }
            public long TransferBytes { get; set; }
            public bool Cached { get; set; }
        }

        // The closed list of what a client tells about its device. 0 or null: unknown.
        [MemoryPackable]
        public partial class Device
        {
            public string Platform { get; set; }
            public string BuildVersion { get; set; }
            public string Browser { get; set; }
            public int BrowserMajor { get; set; }
            public string Os { get; set; }
            public int OsMajor { get; set; }
            public string Gpu { get; set; }
            public int Cores { get; set; }
            public double MemoryClass { get; set; }
            public int ScreenWidth { get; set; }
            public int ScreenHeight { get; set; }
            public double Dpr { get; set; }
        }

        [MemoryPackable]
        public partial class MetricName
        {
            public string Name { get; set; }
            public string Unit { get; set; }
        }

        // Time: milliseconds since the start of the session, here and in Error and Mark. Values by the slots of the names,
        // NaN: no value in this sample, a shorter array leaves the rest empty. Hidden: the page was in the background.
        [MemoryPackable]
        public partial class Sample
        {
            public long Time { get; set; }
            public double[] Values { get; set; }
            public bool Hidden { get; set; }
        }

        public enum ErrorLevel : byte
        {
            Error = 1,
            Exception = 2,

            // A warning of the log of Unity: counted apart from the errors.
            Warning = 3
        }

        [MemoryPackable]
        public partial class Error
        {
            public long Time { get; set; }
            public ErrorLevel Level { get; set; }
            public string Message { get; set; }
            public string Stack { get; set; }
            public int Repeat { get; set; }
        }

        // Detail: a free text of the code (the key of a loaded asset), scrubbed by the server, null for none. DurationMs: what the
        // mark closes (a load), null for none.
        [MemoryPackable]
        public partial class Mark
        {
            public long Time { get; set; }
            public string Name { get; set; }
            public string Detail { get; set; }
            public long? DurationMs { get; set; }
        }
    }
}
