using System;
using System.Collections.Generic;
using System.Linq;
using GamePlay.Players;

namespace GamePlay.Loop
{
    public interface IGameContext
    {
        IGamePlayer Self { get; }
        IReadOnlyList<IGamePlayer> All { get; }

        GameOptions Options { get; }
        bool IsFirstOpened { get; }
        
        void CompleteSetup(IReadOnlyList<IGamePlayer> players);
        void OnFirstOpen();
    }
    
    public static class GameContextExtensions
    {
        public static IGamePlayer GetPlayer(this IGameContext context, Guid id)
        {
            return context.All.First(x => x.Id == id);
        }
    }
}