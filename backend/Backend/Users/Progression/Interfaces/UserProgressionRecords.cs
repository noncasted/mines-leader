namespace Backend.Users;

public class UserProgressionRecords
{
    [GenerateSerializer]
    public class Win : IUserProgressionRecord
    {
        [Id(0)]
        public required DateTime Date { get; init; }

        [Id(1)]
        public required int Experience { get; init; }
        
        public int GetExperience() => Experience;
    }
    
    [GenerateSerializer]
    public class Loss : IUserProgressionRecord
    {
        [Id(0)]
        public required DateTime Date { get; init; }
        
        [Id(1)]
        public required int Experience { get; init; }
        
        public int GetExperience() => Experience;
    }
}