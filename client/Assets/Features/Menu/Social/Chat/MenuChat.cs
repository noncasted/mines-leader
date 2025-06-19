using System;
using Common.Network;
using Common.Network.Common;
using Internal;
using MemoryPack;
using Meta;
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
            IUser user,
            IMenuPlayersCollection playersCollection,
            IMenuChatUI ui)
        {
            _user = user;
            _playersCollection = playersCollection;
            _ui = ui;
        }

        private readonly IUser _user;
        private readonly IMenuPlayersCollection _playersCollection;
        private readonly IMenuChatUI _ui;

        public override void OnStarted(IReadOnlyLifetime lifetime)
        {
            _ui.MessageSend.Advise(lifetime, OnMessageSent);
            Events.GetEvent<MenuChatMessagePayload>().Advise(lifetime, OnMessageReceived);
        }

        private void OnMessageSent(string message)
        {
            _playersCollection.Entries[_user.Id].ChatView.ShowMessage(message);

            Events.Send(new MenuChatMessagePayload()
            {
                PlayerId = _user.Id,
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