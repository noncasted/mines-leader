using System;
using Common.Network;
using Common.Network.Common;
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

    public class MenuChat : NetworkService, IMenuChat
    {
        public MenuChat(
            IUserContext userContext,
            IMenuPlayersCollection playersCollection,
            IMenuChatUI ui)
        {
            _userContext = userContext;
            _playersCollection = playersCollection;
            _ui = ui;
        }

        private readonly IUserContext _userContext;
        private readonly IMenuPlayersCollection _playersCollection;
        private readonly IMenuChatUI _ui;

        public override void OnStarted(IReadOnlyLifetime lifetime)
        {
            _ui.MessageSend.Advise(lifetime, OnMessageSent);
            Events.GetEvent<MenuChatMessagePayload>().Advise(lifetime, OnMessageReceived);
        }

        private void OnMessageSent(string message)
        {
            _playersCollection.Entries[_userContext.Id].ChatView.ShowMessage(message);

            Events.Send(new MenuChatMessagePayload()
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