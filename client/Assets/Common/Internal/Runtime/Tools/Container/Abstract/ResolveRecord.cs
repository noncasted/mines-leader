namespace Internal
{
    public readonly struct ResolveRecord
    {
        public ResolveRecord(int slot, double milliseconds, int frame)
        {
            Slot = slot;
            Milliseconds = milliseconds;
            Frame = frame;
        }

        public readonly int Slot;
        public readonly double Milliseconds;
        public readonly int Frame;
    }
}
