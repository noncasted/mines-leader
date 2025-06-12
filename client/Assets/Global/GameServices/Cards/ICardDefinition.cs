using Shared;
using UnityEngine;

namespace Global.GameServices
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