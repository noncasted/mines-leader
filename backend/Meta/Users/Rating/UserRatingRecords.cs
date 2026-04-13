namespace Meta.Users;

public class UserRatingRecords
{
    [GenerateSerializer]
    public class Win : IUserRatingRecord
    {
        [Id(0)]
        public required DateTime Date { get; init; }

        [Id(1)]
        public required int Rating { get; init; }

        public int GetRating() => Rating;
    }

    [GenerateSerializer]
    public class Loss : IUserRatingRecord
    {
        [Id(0)]
        public required DateTime Date { get; init; }

        [Id(1)]
        public required int Rating { get; init; }

        public int GetRating() => -Rating;
    }

    [GenerateSerializer]
    public class AdminAdjust : IUserRatingRecord
    {
        [Id(0)]
        public required DateTime Date { get; init; }

        [Id(1)]
        public required int Value { get; init; }

        public int GetRating() => Value;
    }
}