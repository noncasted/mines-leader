using System;
using Internal;

namespace Menu
{
    public interface IMenuPlayer
    {
        Guid PlayerId { get; }
        IReadOnlyLifetime Lifetime { get; }
        IMenuPlayerChatView ChatView { get; }
    }
}