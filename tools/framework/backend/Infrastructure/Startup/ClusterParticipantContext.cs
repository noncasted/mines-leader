using Internal;

namespace Infrastructure.Startup;

public interface IClusterParticipantContext
{
    // Shared: the startup tile of the console views them from its circuits while the startup sets them.
    ISharedViewableProperty<bool> IsInitialized { get; }
    ISharedViewableProperty<bool> IsMessagingStarted { get; }
    IReadOnlyList<string> Stages { get; }
    ISharedViewableProperty<int> CurrentStageIndex { get; }

    void SetStages(IReadOnlyList<string> stages);
    void SetStage(string stage);
    void SetMessagingStarted();
    void Initialize();
}

public class ClusterParticipantContext : IClusterParticipantContext
{
    private readonly SharedViewableProperty<bool> _isInitialized = new(false);
    private readonly SharedViewableProperty<bool> _isMessagingStarted = new(false);
    private readonly SharedViewableProperty<int> _currentStageIndex = new(0);

    private List<string> _stages = new();

    public ISharedViewableProperty<bool> IsInitialized => _isInitialized;
    public ISharedViewableProperty<bool> IsMessagingStarted => _isMessagingStarted;
    public IReadOnlyList<string> Stages => _stages;
    public ISharedViewableProperty<int> CurrentStageIndex => _currentStageIndex;

    public void SetStages(IReadOnlyList<string> stages)
    {
        _stages = stages.ToList();
    }

    public void SetStage(string stage)
    {
        var index = _stages.IndexOf(stage);

        if (index < 0)
            throw new InvalidOperationException($"Stage '{stage}' is not one of the startup stages: {string.Join(", ", _stages)}");

        _currentStageIndex.Set(index);
    }

    public void SetMessagingStarted()
    {
        _isMessagingStarted.Set(true);
    }

    public void Initialize()
    {
        _currentStageIndex.Set(_stages.Count);
        _isInitialized.Set(true);
    }
}
