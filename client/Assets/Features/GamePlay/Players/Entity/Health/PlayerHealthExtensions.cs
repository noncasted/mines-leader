namespace GamePlay.Players
{
    public static class PlayerHealthExtensions
    {
        public static void AddCurrent(this IPlayerHealth health, int amount)
        {
            health.SetCurrent(health.Current.Value + amount);
        }
        
        public static void RemoveCurrent(this IPlayerHealth health, int amount)
        {
            health.SetCurrent(health.Current.Value - amount);
        }
    }
}