using System;
using Common.Network;
using Cysharp.Threading.Tasks;
using Global.GameServices;
using Internal;
using MemoryPack;
using UnityEngine;

namespace Menu
{
    [MemoryPackable]
    public partial class MenuChatMessagePayload : IEventPayload
    {
        public Guid PlayerId { get; set; }
        public string Message { get; set; }
    }
    
    public class MenuChat : IMenuChat, INetworkSessionSetupCompleted
    {
        public MenuChat(
            IUserContext userContext,
            INetworkServiceEntityFactory entityFactory,
            IMenuPlayersCollection playersCollection,
            IMenuChatUI ui)
        {
            _userContext = userContext;
            _entityFactory = entityFactory;
            _playersCollection = playersCollection;
            _ui = ui;
        }

        private readonly IUserContext _userContext;
        private readonly INetworkServiceEntityFactory _entityFactory;
        private readonly IMenuPlayersCollection _playersCollection;
        private readonly IMenuChatUI _ui;

        private INetworkEntity _entity;
        
        public async UniTask OnSessionSetupCompleted(IReadOnlyLifetime lifetime)
        {
            _entity = await _entityFactory.Create(lifetime, "chat");
            
            _ui.MessageSend.Advise(lifetime, OnMessageSent);

            _entity.Events.GetEvent<MenuChatMessagePayload>().Advise(lifetime, OnMessageReceived);
        }

        private void OnMessageSent(string message)
        {
            _playersCollection.Entries[_userContext.Id].ChatView.ShowMessage(message);
            
            _entity.Events.Send(new MenuChatMessagePayload()
            {
                PlayerId = _userContext.Id,
                Message = message
            });
        }

        private void OnMessageReceived(MenuChatMessagePayload payload)
        {
            if (_playersCollection.Entries.TryGetValue(payload.PlayerId, out var player) == false)
            {
                Debug.LogWarning($"Player not found: {payload.PlayerId}");
                return;
            }

            player.ChatView.ShowMessage(payload.Message);
        }
    }
}