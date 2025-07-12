using System;
using Common.Network;
using Internal;

namespace Menu.Social
{
    public interface IMenuPlayer
    {
        Guid Id { get; }
        IReadOnlyLifetime Lifetime { get; }
        IMenuPlayerChatView ChatView { get; }
    }

    public class MenuPlayer : IMenuPlayer, IScopeSetup
    {
        public MenuPlayer(
            Guid id,
            INetworkEntity entity,
            MenuPlayerView view,
            IMenuPlayerChatView chatView)
        {
            _entity = entity;
            _view = view;
            Id = id;
            ChatView = chatView;
        }

        private readonly INetworkEntity _entity;
        private readonly MenuPlayerView _view;

        public Guid Id { get; }
        public IReadOnlyLifetime Lifetime => _entity.Lifetime;
        public IMenuPlayerChatView ChatView { get; }
        
        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            Lifetime.Listen(() => _view.Destroy());    
        }
    }
}