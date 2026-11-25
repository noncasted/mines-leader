#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Cysharp.Threading.Tasks;
using Shared;

namespace GamePlay.Agent
{
    // The protocol of an agent match for the editor tools: the same requests a human client sends.
    public interface IGameAgent
    {
        SharedAgentObservation LastObservation { get; }

        SharedAgentObservation State();
        UniTask<SharedAgentObservation> Open(int x, int y);
        UniTask<SharedAgentObservation> Chord(int x, int y);
        UniTask<SharedAgentObservation> Flag(int x, int y);
        UniTask<SharedAgentObservation> Unflag(int x, int y);

        UniTask<SharedAgentObservation> UseCard(
            Guid? cardId,
            string typeName,
            int? x,
            int? y,
            Guid? extraCardId,
            int? chosenIndex);

        UniTask<SharedAgentLegalPlaysResponse> LegalPlays();
        UniTask<SharedAgentObservation> EndTurn(int timeoutMs);
        UniTask<SharedAgentObservation> WaitOwnTurn(int timeoutMs);
        UniTask<SharedAgentObservation> RequestOracleState();
        UniTask<SharedAgentObservation> WaitVisual(int timeoutMs);
        CellInspect InspectCell(int x, int y, bool opponent);
    }
}
#endif
