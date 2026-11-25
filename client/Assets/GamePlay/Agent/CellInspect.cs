#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

namespace GamePlay.Agent
{
    // The live cell view as the agent tool game_inspect_cell returns it.
    public class CellInspect
    {
        public CellInspect(
            int x,
            int y,
            bool exists,
            string state,
            bool flagged,
            int? minesAround,
            IReadOnlyList<string> effects,
            string animator)
        {
            X = x;
            Y = y;
            Exists = exists;
            State = state;
            Flagged = flagged;
            MinesAround = minesAround;
            Effects = effects;
            Animator = animator;
        }

        public int X { get; }
        public int Y { get; }
        public bool Exists { get; }
        public string State { get; }
        public bool Flagged { get; }
        public int? MinesAround { get; }
        public IReadOnlyList<string> Effects { get; }
        public string Animator { get; }

        public static CellInspect Missing(int x, int y)
        {
            return new CellInspect(x, y, false, string.Empty, false, null, new List<string>(), null);
        }
    }
}
#endif
