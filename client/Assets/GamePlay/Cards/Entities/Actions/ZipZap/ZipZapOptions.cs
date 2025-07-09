using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public class ZipZapOptions : EnvAsset
    {
        [SerializeField] private ZipZapLine _linePrefab;

        public ZipZapLine LinePrefab => _linePrefab;
    }
}