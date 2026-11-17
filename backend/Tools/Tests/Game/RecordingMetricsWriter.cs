using FluentAssertions;
using Game.MatchMetrics;
using Game.Session;
using Internal;
using Shared;

namespace Tests.Game;

// One stream: the events and the values of the gauge session.users in the order they come.
public class RecordingMetricsWriter : IMatchMetricsWriter, IMatchMetricsServerStream
{
    public Guid? MatchId { get; private set; }
    public List<RecordedEvent> Events { get; } = new();
    public List<float> Gauges { get; } = new();

    public IMatchMetricsServerStream Open(IReadOnlyLifetime lifetime, Guid matchId)
    {
        if (MatchId != null)
            throw new InvalidOperationException($"A second stream of match {matchId}");

        MatchId = matchId;
        return this;
    }

    public void Event(string name, params SharedMatchMetrics.MatchEventField[] fields)
    {
        SharedMatchMetricNames.IsName(name).Should().BeTrue();
        fields.Should().OnlyContain(field => SharedMatchMetricNames.IsKey(field.Key));
        Events.Add(new RecordedEvent(name, fields));
    }

    public void Gauge(string name, float value)
    {
        name.Should().Be(SessionMetrics.UsersGauge);
        Gauges.Add(value);
    }
}
