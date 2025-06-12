using System.Net.WebSockets;
using Common;

namespace Game;

public interface IUserFactory
{
    IUser Create(IReadOnlyLifetime lifetime, Guid userId, WebSocket webSocket);
}