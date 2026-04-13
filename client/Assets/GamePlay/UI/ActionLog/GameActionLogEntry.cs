using Shared;

namespace GamePlay.UI.ActionLog
{
    public enum GameActionLogEntryType
    {
        CardPlayedSelf,
        CardPlayedOpponent,
    }

    public class GameActionLogEntry
    {
        public GameActionLogEntryType Type { get; set; }
        public string PlayerName { get; set; }
        public CardType CardType { get; set; }
        public string CardName { get; set; }
        public string CardDescription { get; set; }
    }
}