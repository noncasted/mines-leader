using System;
using Internal;
using UnityEngine;

namespace Menu
{
    [DisallowMultipleComponent]
    public class MenuPlayer : MonoBehaviour, IMenuPlayer
    {
        [SerializeField] private MenuPlayerMovement _movement;
        [SerializeField] private MenuPlayerChatView _chatView;

        private Guid _playerId;
        private IReadOnlyLifetime _lifetime;

        public Guid PlayerId => _playerId;
        public IReadOnlyLifetime Lifetime => _lifetime;
        public IMenuPlayerChatView ChatView => _chatView;
        public MenuPlayerMovement Movement => _movement;

        public void Construct(Guid playerId, IReadOnlyLifetime lifetime)
        {
            _lifetime = lifetime;
            _playerId = playerId;
        }
    }
}