using Internal;
using UnityEngine;

namespace GamePlay.Players.Resource
{
    [DisallowMultipleComponent]
    public class PlayerResourceRow : MonoBehaviour
    {
        [SerializeField] private PlayerResourceEntry[] _large;
        [SerializeField] private PlayerResourceEntry[] _upperSmall;
        [SerializeField] private PlayerResourceEntry[] _bottomSmall;

        public void Setup(IReadOnlyLifetime lifetime, IPlayerResource resource, PlayerResourceOptions options)
        {
            resource.Current.View(lifetime, _ => Recalculate(resource, options));
            resource.BaseMax.View(lifetime, _ => Recalculate(resource, options));
            resource.ResultMax.View(lifetime, _ => Recalculate(resource, options));
        }

        private void Recalculate(IPlayerResource resource, PlayerResourceOptions options)
        {
            var current = resource.Current.Value;
            var baseMax = resource.BaseMax.Value;
            var resultMax = resource.ResultMax.Value;

            if (resultMax > _large.Length)
                ApplySmall(current, baseMax, resultMax, options);
            else
                ApplyLarge(current, baseMax, resultMax, options);
        }

        private void ApplyLarge(int current, int baseMax, int resultMax, PlayerResourceOptions options)
        {
            HideAll(_upperSmall);
            HideAll(_bottomSmall);
            
            var additional = resultMax - baseMax;
            var spent = baseMax - current;

            for (var i = 0; i < _large.Length - 1; i++)
            {
                var entry = _large[i];
                
                if (i < resultMax)
                {
                    entry.gameObject.SetActive(true);

                    var isBase = i >= additional;

                    if (i < spent)
                        entry.SetEmpty(options, true, isBase);
                    else
                        entry.SetFull(options, true, isBase);
                }
                else
                {
                    entry.gameObject.SetActive(false);

                }
            }
        }

        private void ApplySmall(int current, int baseMax, int resultMax, PlayerResourceOptions options)
        {
            HideAll(_large);
            HideAll(_upperSmall);
            HideAll(_bottomSmall);

            var additional = resultMax - baseMax;
            var spent = baseMax - current;

            for (var i = 0; i < resultMax; i++)
            {
                var row = i % 2 == 0 ? _upperSmall : _bottomSmall;
                var index = i / 2;

                if (index >= row.Length)
                    continue;

                var entry = row[index];
                entry.gameObject.SetActive(true);

                var isBase = i >= additional;

                if (i < spent)
                    entry.SetEmpty(options, false, isBase);
                else
                    entry.SetFull(options, false, isBase);
            }
        }

        private static void HideAll(PlayerResourceEntry[] entries)
        {
            for (var i = 0; i < entries.Length; i++)
                entries[i].gameObject.SetActive(false);
        }
    }
}