using System.Collections.Generic;
using GamePlay.Loop;
using GamePlay.Players;
using Internal;

namespace Menu.Decks
{
    /// <summary>
    /// Single-player <see cref="IGameContext"/> for the menu preview. Self == Other == the stub
    /// player whose board is the Menu_Board. <c>GetPlayer(Guid)</c> (extension method on IGameContext)
    /// always resolves to this player because card action syncs call GetPlayer by the target player id
    /// from the snapshot — which in preview is always our stub.
    /// </summary>
    public sealed class MenuPreviewGameContext : IGameContext, IScopeSetup
    {
        public MenuPreviewGameContext(IMenuBoard menuBoard)
        {
            _player = new MenuPreviewGamePlayer(menuBoard.Board);
            _all = new List<IGamePlayer> { _player };
        }

        private readonly IGamePlayer _player;
        private readonly List<IGamePlayer> _all;
        private readonly ViewableDelegate _updated = new();

        public IGamePlayer Self => _player;
        public IGamePlayer Other => _player;
        public IReadOnlyList<IGamePlayer> All => _all;
        public IViewableDelegate Updated => _updated;
        public bool IsGameStarted => true;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
        }

        public void AddPlayer(IGamePlayer player)
        {
        }

        public void SetGameStarted()
        {
        }
    }
}
