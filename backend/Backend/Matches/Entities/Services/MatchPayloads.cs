using Infrastructure.Messaging;

namespace Backend.Matches;

public class MatchPayloads
{
    public class Create
    {
        [GenerateSerializer]
        public class Request : IClusterMessage
        {
            [Id(0)]
            public required string Type { get; init; }
            
            [Id(1)]
            public required int ExpectedUsers { get; init; }
        }
        
        [GenerateSerializer]
        public class Response : IClusterMessage
        {
            [Id(0)]
            public required Guid SessionId { get; init; }
        }
    }
    
    public class GetOrCreate
    {
        [GenerateSerializer]
        public class Request : IClusterMessage
        {
            [Id(0)]
            public required string Type { get; init; }
        }
        
        [GenerateSerializer]
        public class Response : IClusterMessage
        {
            [Id(0)]
            public required Guid SessionId { get; init; }
        }
    }
    
}