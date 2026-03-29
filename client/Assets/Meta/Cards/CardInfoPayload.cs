using System;

namespace Meta
{
    [Serializable]
    public class CardInfoPayload
    {
        public string type;
        public string name;
        public string description;
        public string icon;
    }

    [Serializable]
    public class CardsInfoPayload
    {
        public CardInfoPayload[] cards;
    }
}
