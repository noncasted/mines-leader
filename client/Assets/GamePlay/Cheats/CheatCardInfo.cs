#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace GamePlay.Cheats
{
    public readonly struct CheatCardInfo
    {
        public CheatCardInfo(int typeId, string name, Sprite icon)
        {
            TypeId = typeId;
            Name = name;
            Icon = icon;
        }

        public int TypeId { get; }
        public string Name { get; }
        public Sprite Icon { get; }
    }
}
#endif
