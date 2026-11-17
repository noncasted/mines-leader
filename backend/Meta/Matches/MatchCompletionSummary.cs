namespace Meta.Matches;

/// <summary>Итоги матча для экрана результатов: считаются здесь же, где применяются к грейнам игроков.</summary>
[GenerateSerializer]
public class MatchCompletionSummary
{
    [Id(0)] public TimeSpan Duration { get; set; }
    [Id(1)] public Dictionary<Guid, int> RatingChanges { get; set; } = new();
    [Id(2)] public Dictionary<Guid, int> Ratings { get; set; } = new();
}
