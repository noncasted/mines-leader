using System.Collections.Generic;

namespace GamePlay.Boards
{
    public class CellInspect
    {
        public int X { get; set; }
        public int Y { get; set; }
        public bool Exists { get; set; }
        public string State { get; set; }
        public bool Flagged { get; set; }
        public int? MinesAround { get; set; }
        public List<string> Effects { get; set; } = new List<string>();
        public string Animator { get; set; }
        public bool Active { get; set; }
    }
}
