using System;
using Internal;

namespace Menu.Social
{
    public interface IMenuPlayer
    {
        Guid Id { get; }
        IReadOnlyLifetime Lifetime { get; }
        IMenuPlayerChatView ChatView { get; }
    }

    public class MenuPlayer : IMenuPlayer
    {
        public MenuPlayer(Guid id, IReadOnlyLifetime lifetime, IMenuPlayerChatView chatView)
        {
            Id = id;
            Lifetime = lifetime;
            ChatView = chatView;
        }

        public Guid Id { get; }
        public IReadOnlyLifetime Lifetime { get; }
        public IMenuPlayerChatView ChatView { get; }
    }
}