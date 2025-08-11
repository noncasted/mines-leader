using System;
using GamePlay.Services;
using Shared;

namespace GamePlay.Cards
{
    public class CardActionSnapshotHandler : ISnapshotHandler
    {
        public Type Target => typeof(PlayerSnapshotRecord.Card);
        
        public void Handle(IMoveSnapshotRecord record)
        {
            
            
        }
    }
}