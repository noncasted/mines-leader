using System;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Internal
{
    public class Profiler : IProfiler
    {
        public Profiler(string name)
        {
            Name = name;
            _startedAt = DateTime.Now;
            _startFrame = UnityEngine.Time.frameCount;
            _startNs = ProfilerClock.NowNs();
            _watch = Stopwatch.StartNew();
        }

        private readonly DateTime _startedAt;
        private readonly int _startFrame;
        private readonly long _startNs;
        private readonly Stopwatch _watch;
        private readonly List<ProfilerScope> _roots = new();

        private bool _finished;

        public string Name { get; }

        public double ElapsedMs => _watch.Elapsed.TotalMilliseconds;

        /// <summary>Кадр движка, на котором началась трасса. Абсолютный.</summary>
        public int StartFrame => _startFrame;

        /// <summary>Кадров с начала трассы. По ним видно, ждал отрезок кадры или считал.</summary>
        public int ElapsedFrames => UnityEngine.Time.frameCount - _startFrame;

        public IProfilerScope CreateScope(string name)
        {
            var scope = new ProfilerScope(this, name, 0);
            _roots.Add(scope);

            return scope;
        }

        public void Finish()
        {
            if (_finished)
                return;

            _finished = true;

            foreach (var root in _roots)
                root.StopRecursive();

            _watch.Stop();

            var trace = Build();
            ProfilerTraceStorage.Save(trace);

            Debug.Log($"[Profiler] {Name} finished in {trace.DurationMs:F1} ms / {trace.Frames} frames, {trace.Spans.Count} spans");
        }

        public ProfilerTraceData Build()
        {
            var trace = new ProfilerTraceData
            {
                Name = Name,
                StartedAt = _startedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                DurationMs = _watch.Elapsed.TotalMilliseconds,
                Frames = ElapsedFrames,
                StartFrame = _startFrame,
                EndFrame = UnityEngine.Time.frameCount,
                StartNs = _startNs,
                EndNs = ProfilerClock.NowNs(),
                Cold = ProfilerTraceStorage.ColdDomain,
                Spans = new List<ProfilerSpanData>()
            };

            var nextId = 0;

            // Порядок обхода — в глубину по времени старта: так строки в окне ложатся
            // сверху вниз ровно так же, как этапы шли по времени.
            _roots.Sort(CompareByStart);

            foreach (var root in _roots)
                Append(root, -1, 0);

            return trace;

            void Append(ProfilerScope scope, int parentId, int depth)
            {
                var id = nextId++;

                trace.Spans.Add(new ProfilerSpanData
                {
                    Id = id,
                    ParentId = parentId,
                    Depth = depth,
                    Name = scope.Name,
                    StartMs = Math.Max(0d, scope.StartMs),
                    DurationMs = scope.DurationMs,
                    Frames = scope.Frames,
                    StartFrame = scope.StartFrame,
                    EndFrame = scope.EndFrame,
                    StartNs = scope.StartNs,
                    EndNs = scope.EndNs,
                    Unfinished = scope.WasUnfinished
                });

                var children = scope.ChildrenInternal;
                children.Sort(CompareByStart);

                foreach (var child in children)
                    Append(child, id, depth + 1);
            }

            static int CompareByStart(ProfilerScope left, ProfilerScope right)
            {
                return left.StartMs.CompareTo(right.StartMs);
            }
        }
    }
}
