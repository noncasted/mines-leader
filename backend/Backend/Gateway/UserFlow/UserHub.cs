using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Backend.Gateway;

public class UserHub : Hub
{
    public UserHub(IConnectedUsers users, ILogger<UserHub> logger)
    {
        _users = users;
        _logger = logger;
    }

    private readonly IConnectedUsers _users;
    private readonly ILogger<UserHub> _logger;

    public override async Task OnConnectedAsync()
    {
        var rawId = Context.GetHttpContext()?.Request.Query["UserId"];

        if (string.IsNullOrEmpty(rawId) == true)
            throw new Exception("User ID is required");

        var userId = Guid.Parse(rawId.Value.ToString());

        _logger.LogInformation("[User] [Hub] User {Id} connected", userId);

        Context.Items["UserId"] = rawId;
        await base.OnConnectedAsync();
        
        _users.OnConnected(userId, Context.ConnectionId);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var id = Guid.Parse(Context.Items["UserId"]!.ToString()!);

        _logger.LogInformation("[User] [Hub] User {Id} disconnected", id);

        _users.OnDisconnected(id);
        return Task.CompletedTask;
    }
}