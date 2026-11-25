namespace GamePlay.Loop
{
    public enum MatchResultType
    {
        Win = 1,
        Lose = 2,
        Leave = 3,

        // The client could not apply a snapshot of the server (SnapshotReceiver): its state is out of sync, the match ends
        // without results.
        Failed = 4
    }
}
