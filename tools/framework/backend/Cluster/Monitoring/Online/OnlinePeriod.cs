namespace Cluster.Monitoring.Online;

// One hour or one day of the online statistics: the peak of the users online at the same time and the number of
// different users who were online in it.
[GenerateSerializer]
public class OnlinePeriod
{
    [Id(0)] public DateTime Start { get; init; }
    [Id(1)] public int Peak { get; init; }
    [Id(2)] public int UniqueUsers { get; init; }
}
