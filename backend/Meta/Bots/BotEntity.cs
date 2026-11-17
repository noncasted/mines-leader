using Common;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging;
using Shared;

namespace Meta.Bots;

[GenerateSerializer]
[GrainState(Table = "bot_entity", State = "bot_entity", Lookup = "Bot", Key = GrainKeyType.Guid)]
public class BotState : IDirectStateValue
{
    [Id(0)] public Guid Id { get; set; }

    public int Version => 0;
}

public interface IBot : IGrainWithGuidKey
{
    [Transaction]
    Task Initialize();

    [Transaction]
    Task OnUpdated();
}

public class Bot : Grain, IBot
{
    public Bot(
        [State] State<BotState> state,
        IBotCollection collection,
        ILogger<Bot> logger)
    {
        _state = state;
        _collection = collection;
        _logger = logger;
    }

    private readonly State<BotState> _state;
    private readonly IBotCollection _collection;
    private readonly ILogger<Bot> _logger;

    public async Task Initialize()
    {
        var state = await _state.Update(state => {
            state.Id = this.GetPrimaryKey();
        });

        _logger.LogInformation("[Bot] Created bot {Id}", state.Id);
    }

    public async Task OnUpdated()
    {
        var state = await _state.ReadValue();
        await _collection.OnUpdatedTransactional(state.Id, state);
    }
}