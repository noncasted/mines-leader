using Microsoft.Extensions.Logging;

namespace Infrastructure.Orleans;

public class ObserverResubscriber {
    private readonly TimeSpan _time;
    private readonly ILogger _logger;

    public ObserverResubscriber(TimeSpan time, ILogger logger) {
        _time = time;
        _logger = logger;
    }

    public async Task Run(Func<Task> action) {
        while (true) {
            try {
                await action();
            } 
            catch (Exception e) {
                _logger.LogError(e, "Error in observer resubscriber");
            }

            await Task.Delay(_time);
        }
    }
}