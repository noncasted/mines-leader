using Shared;
using UnityEngine;

namespace Meta
{
    public interface IGameModeDefinition
    {
        GameMatchType Type { get; }
        string Name { get; }
        string Description { get; }
        Sprite Image { get; }
    }

    public class GameModeDefinition : IGameModeDefinition
    {
        public GameModeDefinition(GameMatchType type, string name, string description, Sprite image)
        {
            Type = type;
            Name = name;
            Description = description;
            Image = image;
        }

        public GameMatchType Type { get; }
        public string Name { get; }
        public string Description { get; }
        public Sprite Image { get; }
    }
}