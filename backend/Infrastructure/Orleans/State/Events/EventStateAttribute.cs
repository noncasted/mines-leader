namespace Infrastructure.State;

[AttributeUsage(AttributeTargets.Parameter)]
public class EventStateAttribute : Attribute, IFacetMetadata
{
}
