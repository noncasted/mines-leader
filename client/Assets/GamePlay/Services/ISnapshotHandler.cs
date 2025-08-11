using System;
using Shared;

namespace GamePlay.Services
{
    public interface ISnapshotHandler
    {
        Type Target { get; }

        void Handle(IMoveSnapshotRecord record);
    }
}