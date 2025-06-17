using Shared;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Backend.Matches;

[GenerateSerializer]
public class MatchOverview
{
    [Id(0)]
    public required Guid Id { get; init; }
    
    [Id(1)]
    public required List<Guid> Participants { get; init; }
    
    [Id(2)]
    public required DateTime Date { get; init; }
    
    [Id(3)]
    public required Guid Winner { get; init; }
    
    [Id(4)]
    public required TimeSpan Time { get; init; }
    
    [Id(5)]
    public required GameMatchType Type { get; init; }
}