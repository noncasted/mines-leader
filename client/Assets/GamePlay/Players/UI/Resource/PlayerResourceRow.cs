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

        public void Setup(
            IReadOnlyLifetime lifetime,
            IPlayerResource resource,
            PlayerResourceOptions options,
            bool reverse)
        {
            resource.Current.View(lifetime, _ => Recalculate(resource, options, reverse));
            resource.BaseMax.View(lifetime, _ => Recalculate(resource, options, reverse));
            resource.ResultMax.View(lifetime, _ => Recalculate(resource, options, reverse));
        }

        private void Recalculate(IPlayerResource resource, PlayerResourceOptions options, bool reverse)
        {
            var current = resource.Current.Value;
            var baseMax = resource.BaseMax.Value;
            var resultMax = resource.ResultMax.Value;

            if (resultMax > _large.Length)
                ApplySmall(current, baseMax, resultMax, options, reverse);
            else
                ApplyLarge(current, baseMax, resultMax, options, reverse);
        }

        private void ApplyLarge(
            int current,
            int baseMax,
            int resultMax,
            PlayerResourceOptions options,
            bool reverse)
        {
            HideAll(_upperSmall);
            HideAll(_bottomSmall);
            
            // Пипсы рисуются по resultMax, поэтому и потраченные считаются от него:
            // при бонусе current больше baseMax, и baseMax - current уходил в минус,
            // из-за чего ни один пипс не гасился и бафф выглядел на ход щедрее.
            var additional = resultMax - baseMax;
            var spent = resultMax - current;

            for (var i = 0; i < _large.Length; i++)
            {
                var entry = _large[i];
                
                if (i < resultMax)
                {
                    entry.gameObject.SetActive(true);

                    var slot = SlotIndex(i, resultMax, reverse);
                    var isBase = slot >= additional;

                    if (slot < spent)
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

        private void ApplySmall(
            int current,
            int baseMax,
            int resultMax,
            PlayerResourceOptions options,
            bool reverse)
        {
            HideAll(_large);
            HideAll(_upperSmall);
            HideAll(_bottomSmall);

            var additional = resultMax - baseMax;
            var spent = resultMax - current;

            for (var i = 0; i < resultMax; i++)
            {
                var row = i % 2 == 0 ? _upperSmall : _bottomSmall;
                var index = i / 2;

                if (index >= row.Length)
                    continue;

                var entry = row[index];
                entry.gameObject.SetActive(true);

                var slot = SlotIndex(i, resultMax, reverse);
                var isBase = slot >= additional;

                if (slot < spent)
                    entry.SetEmpty(options, false, isBase);
                else
                    entry.SetFull(options, false, isBase);
            }
        }

        private static int SlotIndex(int i, int resultMax, bool reverse)
        {
            if (reverse == true)
                return resultMax - 1 - i;

            return i;
        }

        private static void HideAll(PlayerResourceEntry[] entries)
        {
            for (var i = 0; i < entries.Length; i++)
                entries[i].gameObject.SetActive(false);
        }
    }
}