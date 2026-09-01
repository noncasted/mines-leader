using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    public class ProfilerScope : IProfilerScope
    {
        public ProfilerScope(Profiler profiler, string name, int depth)
        {
            _profiler = profiler;
            Name = name;
            Depth = depth;
        }

        private readonly Profiler _profiler;
        private readonly List<ProfilerScope> _children = new();

        private double _startMs = -1d;
        private double _endMs = -1d;

        private int _startFrame = -1;
        private int _endFrame = -1;

        private long _startNs;
        private long _endNs;

        public string Name { get; private set; }
        public int Depth { get; }

        /// <summary>
        /// Кадр движка, на котором отрезок открылся. Абсолютный (<see cref="UnityEngine.Time.frameCount"/>),
        /// а не от старта трассы: по нему редактор достаёт из профайлера сэмплы ровно тех кадров,
        /// которые занял скоуп.
        /// </summary>
        public int StartFrame => _startFrame;

        /// <summary>Кадр, на котором отрезок закрылся. -1, пока скоуп не остановлен.</summary>
        public int EndFrame => _endFrame;

        /// <summary>Границы отрезка по часам профайлера, нс: по ним редактор находит его кадры.</summary>
        public long StartNs => _startNs;
        public long EndNs => _endNs;

        public bool IsRunning => _startMs >= 0d && _endMs < 0d;

        public double StartMs => _startMs;

        public double DurationMs
        {
            get
            {
                if (_startMs < 0d)
                    return 0d;

                var end = _endMs < 0d ? _profiler.ElapsedMs : _endMs;

                return end - _startMs;
            }
        }

        public int Frames
        {
            get
            {
                if (_startFrame < 0)
                    return 0;

                var end = _endFrame < 0 ? Time.frameCount : _endFrame;

                return end - _startFrame;
            }
        }

        /// <summary>Скоуп, который не закрыли руками: закрыт на Finish, длительность приблизительная.</summary>
        public bool WasUnfinished { get; private set; }

        public IReadOnlyList<IProfilerScope> Children => _children;

        internal List<ProfilerScope> ChildrenInternal => _children;

        public IProfilerScope Child(string name)
        {
            var child = new ProfilerScope(_profiler, name, Depth + 1);
            _children.Add(child);

            return child;
        }

        public void SetName(string name)
        {
            Name = name;
        }

        public void Start()
        {
            if (_startMs >= 0d)
                return;

            _startMs = _profiler.ElapsedMs;
            _startFrame = Time.frameCount;
            _startNs = ProfilerClock.NowNs();
        }

        public void Stop()
        {
            if (_startMs < 0d || _endMs >= 0d)
                return;

            _endMs = _profiler.ElapsedMs;
            _endFrame = Time.frameCount;
            _endNs = ProfilerClock.NowNs();
        }

        public void Dispose()
        {
            Stop();
        }

        internal void StopRecursive()
        {
            if (IsRunning)
            {
                WasUnfinished = true;
                Stop();
            }

            foreach (var child in _children)
                child.StopRecursive();
        }
    }
}
