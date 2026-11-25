using Shared;

namespace Menu.Common
{
    public class GameLoadData
    {
        public GameLoadData(SharedMatchmaking.MatchResult result)
        {
            Result = result;
        }

        public SharedMatchmaking.MatchResult Result { get; }
    }
}