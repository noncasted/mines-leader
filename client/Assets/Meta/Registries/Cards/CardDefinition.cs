using Shared;
using UnityEngine;

namespace Meta
{
    public interface ICardDefinition
    {
        CardType Type { get; }
        CardGroup Group { get; }
        string Name { get; }
        string Description { get; }
        Sprite Image { get; }
    }

    public class CardDefinition : ICardDefinition
    {
        public CardDefinition(CardType type, CardGroup group, string name, string description, Sprite image)
        {
            Type = type;
            Group = group;
            Name = name;
            Description = description;
            Image = image;
        }

        public CardType Type { get; }
        public CardGroup Group { get; }
        public string Name { get; }
        public string Description { get; }
        public Sprite Image { get; }
    }
}