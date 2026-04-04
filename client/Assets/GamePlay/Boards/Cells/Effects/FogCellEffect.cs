using Internal;
using UnityEngine;

namespace GamePlay.Boards.Effects
{
    [DisallowMultipleComponent]
    public class FogCellEffect : CellEffect
    {
        public override void Activate(IReadOnlyLifetime lifetime)
        {
            gameObject.SetActive(true);
            lifetime.Listen(() => gameObject.SetActive(false));
        }
    }
}