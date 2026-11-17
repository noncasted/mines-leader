using System.Collections.Generic;
using Shared;

namespace GamePlay.Metrics
{
    // The tag value of a card: the name of its CardType in lower case (Trebuchet_Max -> trebuchet_max), made once per type.
    public static class CardTagNames
    {
        private static readonly Dictionary<CardType, string> _names = new();

        public static string Get(CardType type)
        {
            if (_names.TryGetValue(type, out var name) == false)
            {
                name = type.ToString().ToLowerInvariant();
                _names.Add(type, name);
            }

            return name;
        }
    }
}
