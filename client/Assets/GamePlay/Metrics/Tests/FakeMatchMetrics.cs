using System;
using System.Collections.Generic;
using Global.MatchMetrics;
using Internal;

namespace GamePlay.Metrics.Tests
{
    // IMatchMetrics of a test: one stream that keeps the sums of its counters by name and tags and its events; a write
    // after the end of its lifetime throws, as the real stream does.
    public class FakeMatchMetrics : IMatchMetrics, IMatchMetricsStream
    {
        public readonly Dictionary<string, Series> All = new();
        public readonly List<(string name, MatchEventField[] fields)> Events = new();

        public bool IsEnded { get; private set; }

        public IMatchMetricsStream Open(IReadOnlyLifetime lifetime, Guid matchId, IMatchMetricsClock clock)
        {
            lifetime.Listen(() => IsEnded = true);
            return this;
        }

        public IMatchCounter Counter(string name, MatchTags tags = default) => Get(tags.ToKey(name));
        public IMatchGauge Gauge(string name, MatchTags tags = default) => Get(tags.ToKey(name));

        public void Event(string name, params MatchEventField[] fields)
        {
            Check();
            Events.Add((name, fields));
        }

        // The sum of a counter by its name and tags written as "name key=value".
        public double Value(string name, string tagKey = null, string tagValue = null)
        {
            var tags = tagKey == null ? default : MatchTags.Of(tagKey, tagValue);
            return All.TryGetValue(tags.ToKey(name), out var series) ? series.Value : 0d;
        }

        private Series Get(string key)
        {
            Check();

            if (All.TryGetValue(key, out var series) == false)
            {
                series = new Series(this);
                All.Add(key, series);
            }

            return series;
        }

        private void Check()
        {
            if (IsEnded == true)
                throw new InvalidOperationException("[Test] A write into an ended stream");
        }

        public class Series : IMatchCounter, IMatchGauge
        {
            public Series(FakeMatchMetrics owner)
            {
                _owner = owner;
            }

            private readonly FakeMatchMetrics _owner;

            public double Value { get; private set; }

            public void Add(float value)
            {
                _owner.Check();
                Value += value;
            }

            public void Set(float value)
            {
                _owner.Check();
                Value = value;
            }
        }
    }
}
