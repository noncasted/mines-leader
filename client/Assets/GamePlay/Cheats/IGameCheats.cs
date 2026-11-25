#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

namespace GamePlay.Cheats
{
    // Cheat requests of the match for the editor window; the server runs them only with the cluster flag Cheats.
    public interface IGameCheats
    {
        IReadOnlyList<CheatCardInfo> GetCards();

        void ChangeHealth(int delta);
        void ChangeMaxHealth(int delta);
        void SetMaxHealth(int value);
        void RestoreHealth();

        void ChangeMana(int delta);
        void ChangeMaxMana(int delta);
        void SetMaxMana(int value);
        void RestoreMana();

        void ChangeMoves(int delta);
        void ChangeMaxMoves(int delta);
        void SetMaxMoves(int value);
        void RestoreMoves();

        void AddCard(int typeId);
        void EndMatch(bool win);
    }
}
#endif
