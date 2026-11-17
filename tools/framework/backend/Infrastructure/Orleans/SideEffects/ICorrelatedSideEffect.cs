namespace Infrastructure;

public interface ICorrelatedSideEffect
{
    Guid CorrelationId { get; }
}
