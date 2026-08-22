using System;
using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class SharedAgentLegalPlaysRequest : INetworkContext
    {
    }

    [MemoryPackable]
    public partial class SharedAgentLegalPlaysResponse : INetworkContext
    {
        public bool IsOwnTurn { get; set; }
        public List<AgentLegalCardView> Cards { get; set; } = new List<AgentLegalCardView>();
    }

    [MemoryPackable]
    public partial class AgentLegalCardView
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public int ManaCost { get; set; }
        public bool NeedsPosition { get; set; }
        public bool NeedsExtraCard { get; set; }
        public bool NeedsChosenIndex { get; set; }
        public List<AgentLegalCell> Cells { get; set; } = new List<AgentLegalCell>();
        public List<Guid> ExtraCardIds { get; set; } = new List<Guid>();
        public string Error { get; set; } = string.Empty;
    }

    [MemoryPackable]
    public partial class AgentLegalCell
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
}
