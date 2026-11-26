using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Meta.Matches;

public static class MatchSummariesExtensions
{
    // The silo (the grain of a match writes it) and the hosts that list the matches (meta, console).
    public static IHostApplicationBuilder AddMatchSummaries(this IHostApplicationBuilder builder)
    {
        builder.AddStateCollection<MatchSummaryCollection, Guid, MatchState>()
               .As<IMatchSummaryCollection>();

        return builder;
    }
}
