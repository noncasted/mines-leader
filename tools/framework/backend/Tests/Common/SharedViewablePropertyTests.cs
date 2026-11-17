using Framework.Tests.Fixtures;
using Internal;
using Xunit;

namespace Framework.Tests.Common;

public class SharedViewablePropertyTests
{
    // The same run over a plain ViewableProperty (what the console aggregator used) throws NullReferenceException from its list.
    [Fact]
    public async Task ConcurrentSubscriptions_WhileTheValueIsSet_LoseNothing()
    {
        var property = new SharedViewableProperty<int>(0);

        await SubscriptionStress.Run<int>((lifetime, listener) => property.View(lifetime, listener), property.Set, value => value);
    }
}
