using Game.Session;

namespace Benchmarks;

public class TestPropertyUpdateSender : IPropertyUpdateSender
{
    public static readonly TestPropertyUpdateSender Instance = new();

    public void Send(int objectId, IObjectProperty property)
    {
        // No-op for tests — no network sync needed
    }
}

public static class TestValuePropertyExtensions
{
    public static ValueProperty<T> ForTest<T>(this ValueProperty<T> property) where T : new()
    {
        property.Construct(TestPropertyUpdateSender.Instance, 0);
        return property;
    }
}
