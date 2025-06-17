using Shared;
using UnityEngine;

namespace Assets.Meta
{
    public interface ICardDefinition
    {
        CardType Type { get; }
        CardTarget Target { get; }
        int ManaCost { get; }
        string Name { get; }
        string Description { get; }
        Sprite Image { get; }
    }
}