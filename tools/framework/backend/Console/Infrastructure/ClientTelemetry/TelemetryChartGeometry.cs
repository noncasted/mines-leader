using System.Globalization;

namespace Console.Infrastructure.ClientTelemetry;

// From the times and values of a session to the viewBox of a chart. The time goes from 0 to the end of the session, the same for
// all charts of a session.
public sealed class TelemetryChartGeometry
{
    public TelemetryChartGeometry(long end, double min, double max)
    {
        _end = end;
        _min = min;
        _max = max;
    }

    public const double Width = 1000;
    public const double Height = 100;

    private const double _dotLength = 0.1;

    private readonly long _end;
    private readonly double _min;
    private readonly double _max;

    public double X(long time)
    {
        return Width * time / _end;
    }

    // The screen goes down, the value goes up. A flat line stands in the middle.
    public double Y(double value)
    {
        return _max == _min ? Height / 2 : Height * (_max - value) / (_max - _min);
    }

    public string Path(IReadOnlyList<TelemetryChartPoint> points)
    {
        var first = $"M{Number(X(points[0].Time))} {Number(Y(points[0].Value))}";

        if (points.Count == 1)
            return $"{first} h{Number(_dotLength)}";

        return first + string.Concat(points.Skip(1).Select(point => $" L{Number(X(point.Time))} {Number(Y(point.Value))}"));
    }

    public static string Number(double value)
    {
        return Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);
    }
}
