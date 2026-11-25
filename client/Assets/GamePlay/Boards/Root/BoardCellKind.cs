namespace GamePlay.Boards
{
    // Player boards share one base cell prefab and differ only by sprites,
    // so the board picks the variant by its ownership.
    public enum BoardCellKind
    {
        Own = 0,
        Opponent = 1
    }
}
