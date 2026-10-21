namespace Console;

// Pages of the game console. The infrastructure pages are in ConsoleConstants of the framework console.
public static class GameConsoleConstants
{
    public static class Pages
    {
        public const string Benchmarks = "/benchmarks";
        public const string Configs = "/configs";
        public const string Bots = "/bots";
        public const string BotEditor = "/bots/{0}";
        public const string Users = "/users";
        public const string UserDashboard = "/users/{0}";
        public const string Match = "/match/{0}";
        public const string LiveMatches = "/matches";
        public const string Matchmaking = "/matchmaking";
        public const string MatchHistory = "/match-history";
    }
}
