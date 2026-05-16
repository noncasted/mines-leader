using System.Collections.Generic;

namespace Shared
{
    [SharedGrainState(Table = "configs",
        State = "user_deck_config",
        Lookup = "UserDeckConfig",
        Key = GrainKeyType.String)]
    public class UserDeckConfigOptions
    {
        public List<CardType> BaseDeck { get; set; } = new();
    }
}
