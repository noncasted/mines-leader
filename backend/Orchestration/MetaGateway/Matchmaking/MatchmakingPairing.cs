using MetaGateway.UserFlow;
using Shared;

namespace MetaGateway.Matchmaking;

// One pass over the queues: a player waiting past the threshold gets a bot, the first two connected players of a
// queue are matched, a disconnected player leaves the queue.
public static class MatchmakingPairing
{
    public static bool Run(
        Dictionary<GameMatchType, List<SearchQueueEntry>> queues,
        DateTime now,
        double thresholdSeconds,
        IConnectedUsers users,
        ILogger<Matchmaking> logger,
        List<(SearchQueueEntry[], GameMatchType)> matchesCreated,
        List<(SearchQueueEntry, GameMatchType)> botMatchesCreated)
    {
        var hasMatched = false;

        foreach (var (type, queue) in queues)
        {
            var toRemove = new List<int>();
            var botMatches = new List<SearchQueueEntry>();

            for (var i = 0; i < queue.Count; i++)
            {
                var entry = queue[i];
                var waitTimeSeconds = (now - entry.JoinedAt).TotalSeconds;

                if (waitTimeSeconds >= thresholdSeconds)
                {
                    if (users.IsConnected(entry.UserId) == true)
                    {
                        logger.LogInformation("[Matchmaking] {UserID} waited {WaitTime}s, applying bot match",
                            entry.UserId,
                            (int)waitTimeSeconds);
                        botMatches.Add(entry);
                        toRemove.Add(i);
                    }
                    else
                    {
                        logger.LogInformation("[Matchmaking] {UserID} is not connected", entry.UserId);
                        toRemove.Add(i);
                    }
                }
            }

            foreach (var idx in toRemove.OrderByDescending(x => x))
                queue.RemoveAt(idx);

            if (queue.Count >= 2)
            {
                var first = queue[0];
                var second = queue[1];

                if (users.IsConnected(first.UserId) == false)
                {
                    logger.LogInformation("[Matchmaking] {UserID} is not connected", first.UserId);
                    queue.RemoveAt(0);
                    hasMatched = true;
                    continue;
                }

                if (users.IsConnected(second.UserId) == false)
                {
                    logger.LogInformation("[Matchmaking] {UserID} is not connected", second.UserId);
                    queue.RemoveAt(1);
                    hasMatched = true;
                    continue;
                }

                queue.RemoveAt(0);
                queue.RemoveAt(0);

                logger.LogInformation("[Matchmaking] {First} and {Second} are matched",
                    first.UserId,
                    second.UserId);

                matchesCreated.Add((new[] { first, second }, type));
                hasMatched = true;
            }
            else if (botMatches.Count > 0)
            {
                foreach (var entry in botMatches)
                    botMatchesCreated.Add((entry, type));

                hasMatched = true;
            }
        }

        return hasMatched;
    }
}
