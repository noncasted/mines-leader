using System;
using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    [MemoryPackable]
    public partial class SharedAgentObservation : INetworkContext
    {
        public int Sequence { get; set; }
        public int EventCursor { get; set; }
        public bool IsOwnTurn { get; set; }
        public bool GameOver { get; set; }
        public Guid WinnerId { get; set; }
        public string WinReason { get; set; } = string.Empty;
        public string Trigger { get; set; } = string.Empty;
        public bool HasError { get; set; }
        public string Error { get; set; } = string.Empty;
        public List<string> Events { get; set; } = new List<string>();
        public AgentPlayerView Self { get; set; } = new AgentPlayerView();
        public AgentPlayerView Opponent { get; set; } = new AgentPlayerView();
    }

    [MemoryPackable]
    public partial class AgentPlayerView
    {
        public Guid Id { get; set; }
        public int Health { get; set; }
        public int HealthMax { get; set; }
        public int Mana { get; set; }
        public int ManaMax { get; set; }
        public int MovesLeft { get; set; }
        public int MovesMax { get; set; }
        public bool MovesAvailable { get; set; }
        public int Mines { get; set; }
        public int Flags { get; set; }
        public List<string> Modifiers { get; set; } = new List<string>();
        public List<AgentCardView> Hand { get; set; } = new List<AgentCardView>();
        public int DeckCount { get; set; }
        public int StashCount { get; set; }
        public string BoardAscii { get; set; } = string.Empty;
        public List<AgentCellView> Cells { get; set; } = new List<AgentCellView>();
    }

    [MemoryPackable]
    public partial class AgentCardView
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public int ManaCost { get; set; }

        /// <summary>OwnBoard | OpponentBoard | Self | Opponent.</summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>Rhombus | Line | Cross | Chain | Single | None. See <see cref="AgentCardCatalog"/>.</summary>
        public string Shape { get; set; } = string.Empty;

        /// <summary>Pattern size from config; 0 when Shape is None. Random sizes report the maximum.</summary>
        public int Size { get; set; }

        public bool NeedsPosition { get; set; }

        /// <summary>One English line describing the effect and the required target cells.</summary>
        public string Summary { get; set; } = string.Empty;
    }

    [MemoryPackable]
    public partial class AgentCellView
    {
        public int X { get; set; }
        public int Y { get; set; }
        public string Status { get; set; } = string.Empty;
        public int MinesAround { get; set; }
        public List<string> Effects { get; set; } = new List<string>();
        public bool HasMine { get; set; }
    }
}
