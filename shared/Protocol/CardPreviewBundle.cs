using System.Collections.Generic;
using MemoryPack;

namespace Shared
{
    /// <summary>
    /// Compact representation of a board cell state used in card preview bundles.
    /// Mirrors the DSL from <c>BoardParser</c> — taken/free/mine/flag flags.
    /// </summary>
    public enum PreviewCellKind : byte
    {
        Taken = 0,
        TakenMine = 1,
        TakenFlag = 2,
        TakenFlagMine = 3,
        Free = 4
    }

    [MemoryPackable]
    public partial struct PreviewCell
    {
        public Position Position { get; set; }
        public PreviewCellKind Kind { get; set; }
        public int MinesAround { get; set; }
    }

    /// <summary>
    /// Snapshot of an initial board layout ready to be rendered on the preview menu board.
    /// </summary>
    [MemoryPackable]
    public partial class BoardLayoutSnapshot
    {
        public int Size { get; set; }
        public IReadOnlyList<PreviewCell> Cells { get; set; }
    }

    /// <summary>
    /// Card preview bundle — initial board layout plus recorded card action snapshots.
    /// The client applies the initial layout to the menu board, then replays the action
    /// sequence through the existing <see cref="ICardActionData"/> pipeline.
    /// </summary>
    [MemoryPackable]
    public partial class CardPreviewBundle
    {
        public CardType CardType { get; set; }
        public Position Target { get; set; }
        public BoardLayoutSnapshot InitialState { get; set; }
        public BoardLayoutSnapshot FinalState { get; set; }
        public IReadOnlyList<ICardActionData> Actions { get; set; }
    }

    /// <summary>
    /// Top-level projection payload delivered to every client on connect.
    /// Stored client-side in a single-writer cache and consumed by the menu card preview UI.
    /// </summary>
    [MemoryPackable]
    public partial class InitialCardPreviews : INetworkContext
    {
        public IReadOnlyList<CardPreviewBundle> Bundles { get; set; }
    }
}
