using System.Globalization;
using System.Text;

namespace Meta.Matches;

// The series of a player as csv for a table or a script: a row per point, "time,series,value", by the series and the
// time; numbers of the invariant culture, "\n", no quotes (the keys are of [a-z0-9._:-]).
public static class MatchPlayerSeriesCsv
{
    public const string Header = "time,series,value";

    public static string Write(MatchPlayerSeries series)
    {
        var text = new StringBuilder();
        text.Append(Header).Append('\n');

        foreach (var line in series.Lines)
        {
            foreach (var point in line.Points)
            {
                text.Append(point.Time.ToString(CultureInfo.InvariantCulture))
                    .Append(',')
                    .Append(line.Key)
                    .Append(',')
                    .Append(point.Value.ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
            }
        }

        return text.ToString();
    }
}
