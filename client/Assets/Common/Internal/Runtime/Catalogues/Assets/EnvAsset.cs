using UnityEngine;

namespace Internal
{
    // Маркер ассета, попадающего в каталог. Собственного состояния не несёт:
    // идентичность ассета — это его группа и имя в каталоге.
    public abstract class EnvAsset : ScriptableObject
    {
    }
}