namespace Common
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class GrainEventStateAttribute : Attribute
    {
        public required string State { get; init; }
        public required string Lookup { get; init; }
        public required GrainKeyType Key { get; init; }
    }
}
