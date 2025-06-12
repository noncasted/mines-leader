using GamePlay.Players;
using Internal;

namespace GamePlay.Loop
{
    public interface IGameRound
    {
        bool IsTurnAllowed { get; }
        IViewableProperty<IGamePlayer> Player { get; }
        IViewableProperty<float> RoundTime { get; }
        
        void Start();
        void OnLocalTurnCompleted();
        void TrySkip();
    }
}