using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Boards
{
    public class BoardActions : IBoardActions
    {
        public BoardActions(INetworkConnection connection)
        {
            _connection = connection;
        }

        private readonly INetworkConnection _connection;

        public void Flag(Vector2Int position)
        {
            _connection.Request(new SharedGameAction.SetFlag
            {
                Position = position.ToPosition()
            });
        }

        public void Unflag(Vector2Int position)
        {
            _connection.Request(new SharedGameAction.RemoveFlag
            {
                Position = position.ToPosition()
            });
        }

        public void Open(Vector2Int position)
        {
            _connection.Request(new SharedGameAction.Open
            {
                Position = position.ToPosition()
            });
        }

        public void OpenMultiple(Vector2Int position)
        {
            _connection.Request(new SharedGameAction.OpenMultiple
            {
                Position = position.ToPosition()
            });
        }
    }
}
