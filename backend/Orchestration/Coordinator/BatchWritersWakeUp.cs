using Infrastructure;
using Infrastructure.Execution;

namespace Coordinator;

public class BatchWritersWakeUp
{
    public BatchWritersWakeUp(IOrleans orleans, ILogger<BatchWritersWakeUp> logger)
    {
        _orleans = orleans;
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly ILogger<BatchWritersWakeUp> _logger;

    private readonly IReadOnlyDictionary<string, string> _stateToNamespace = new Dictionary<string, string>()
    {
        { States.Messaging_Queue, typeof(MessageQueue).FullName! }
    };

    public async Task Execute()
    {
        _logger.LogInformation("[Coordinator] [WakeUp] Started");

        foreach (var (state, grainNamespace) in _stateToNamespace)
        {
            _logger.LogInformation("[Coordinator] [WakeUp] {Grain}", grainNamespace);

            var reader = _orleans.CreateDbReader(state)
                .WhereType(state)
                .SelectExtension();

            var count = await reader.Count();

            _logger.LogInformation("[Coordinator] [WakeUp] {Grain} Found {Count} records", grainNamespace, count);

            await foreach (var entry in reader.Read())
            {
                var grain = _orleans.Grains.GetGrain<IBatchWriter>(entry.Extension, grainNamespace);
                await grain.Start();
            }

            _logger.LogInformation("[Coordinator] [WakeUp] {Grain} Finished", grainNamespace);
        }
    }
}