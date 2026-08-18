using System;

namespace Meta
{
    [Serializable]
    public class CardInfoPayload
    {
        public string type;
        public string name;
        public string description;
    }

    [Serializable]
    public class CardsInfoPayload
    {
        public CardInfoPayload[] cards;
    }
}