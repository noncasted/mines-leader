using GamePlay.Players;
using Internal;

namespace GamePlay.Loop
{
    public interface ILocalPlayerCreated
    {
        void OnLocalPlayer(IReadOnlyLifetime lifetime, IGamePlayer player);
    }

    public interface IRemotePlayerCreated
    {
        void OnRemotePlayer(IReadOnlyLifetime lifetime, IGamePlayer other);
    }

    public interface IPlayersCreated
    {
        void OnPlayersCreated(IReadOnlyLifetime lifetime, IGamePlayer self, IGamePlayer other);
    }

    public interface IGameStarted
    {
        void OnGameStarted(IReadOnlyLifetime lifetime);
    }
}