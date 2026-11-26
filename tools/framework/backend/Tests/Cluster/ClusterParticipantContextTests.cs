using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure.Startup;
using Internal;
using Xunit;

namespace Framework.Tests.Cluster;

public class ClusterParticipantContextTests
{
    [Fact]
    public void SetStage_Listed_BecomesCurrent()
    {
        var context = new ClusterParticipantContext();
        context.SetStages(["Orleans", "Messaging"]);

        context.SetStage("Messaging");

        context.CurrentStageIndex.Value.Should().Be(1);
    }

    [Fact]
    public void SetStage_Unknown_Throws()
    {
        var context = new ClusterParticipantContext();
        context.SetStages(["Orleans", "Messaging"]);

        var set = () => context.SetStage("Missing");

        set.Should().Throw<InvalidOperationException>().WithMessage("*Missing*");
    }

    // The startup tile of the console views the stage from its circuits while the startup of the host sets it.
    [Fact]
    public async Task ConcurrentViews_WhileStagesAreSet_LoseNothing()
    {
        var context = new ClusterParticipantContext();
        context.SetStages(Enumerable.Range(0, 5000).Select(index => $"Stage{index}").ToList());

        await SubscriptionStress.Run<int>(
            (subscriber, listener) => context.CurrentStageIndex.View(subscriber, listener),
            index => context.SetStage($"Stage{index}"),
            index => index);
    }
}
