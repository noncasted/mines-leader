namespace GamePlay.Cards
{
    /// <summary>
    /// Render layering shared by every card. All cards live on the overlay layer,
    /// so the dropped pile is kept under the hand by sorting order alone: the pile
    /// grows from <see cref="DroppedOrder"/> upwards and never reaches the hand band.
    /// </summary>
    public static class CardSorting
    {
        public const string OverlayLayer = "UI";

        public const int DroppedOrder = 100;
        public const int HandOrder = 10000;
        public const int SelectedOrder = 20000;
    }
}
