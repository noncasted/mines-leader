namespace GamePlay.Players
{
    public static class PlayerManaExtensions
    {
        public static void AddCurrent(this IPlayerMana mana, int amount)
        {
            mana.SetCurrent(mana.Current.Value + amount);
        }
        
        public static void RemoveCurrent(this IPlayerMana mana, int amount)
        {
            mana.SetCurrent(mana.Current.Value - amount);
        }
    }
}