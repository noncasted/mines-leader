using Cysharp.Threading.Tasks;
using Global.GameServices;
using Internal;
using Shared;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardFactory
    {
        UniTask Create(IReadOnlyLifetime lifetime, CardType type, Vector2 position);
    }
}