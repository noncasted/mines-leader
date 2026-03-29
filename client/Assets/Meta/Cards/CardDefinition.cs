using Shared;
using UnityEngine;

namespace Meta
{
    public interface ICardDefinition
    {
        CardType Type { get; }
        string Name { get; }
        string Description { get; }
        Sprite Image { get; }
    }
    
    public class CardDefinition : ICardDefinition
    {
        public CardDefinition(CardType type, string name, string description, Sprite image)
        {
            Type = type;
            Name = name;
            Description = description;
            Image = image;
        }

        public CardType Type { get; }
        public string Name { get; }
        public string Description { get; }
        public Sprite Image { get; }
    }
}
