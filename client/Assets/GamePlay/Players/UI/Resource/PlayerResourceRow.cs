using Internal;
using UnityEngine;

namespace GamePlay.Players.Resource
{
    [DisallowMultipleComponent]
    public class PlayerResourceRow : MonoBehaviour
    {
        // Пипсы приходят из сгенерированных биндингов ряда: сериализованных массивов больше нет,
        // поэтому и рассинхрона между иерархией и ссылками в инспекторе тоже.
        private Elements _elements;

        public void Setup(
            IReadOnlyLifetime lifetime,
            IPlayerResource resource,
            PlayerResourceOptions options,
            Elements elements,
            bool reverse)
        {
            _elements = elements;

            resource.Current.View(lifetime, _ => Recalculate(resource, options, reverse));
            resource.BaseMax.View(lifetime, _ => Recalculate(resource, options, reverse));
            resource.ResultMax.View(lifetime, _ => Recalculate(resource, options, reverse));
        }

        private void Recalculate(IPlayerResource resource, PlayerResourceOptions options, bool reverse)
        {
            var current = resource.Current.Value;
            var baseMax = resource.BaseMax.Value;
            var resultMax = resource.ResultMax.Value;

            if (resultMax > _elements.Large.Length)
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
            // Какой из рядов виден, решает сам ряд: в иерархии оба выключены, и включать их
            // руками по префабам — ровно тот рассинхрон, от которого ушли вместе с массивами.
            _elements.SmallRoot.SetActive(false);
            _elements.LargeRoot.SetActive(true);

            // Пипсы рисуются по resultMax, поэтому и потраченные считаются от него:
            // при бонусе current больше baseMax, и baseMax - current уходил в минус,
            // из-за чего ни один пипс не гасился и бафф выглядел на ход щедрее.
            var additional = resultMax - baseMax;
            var spent = resultMax - current;

            var large = _elements.Large;

            for (var i = 0; i < large.Length; i++)
            {
                var entry = large[i];

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
            _elements.LargeRoot.SetActive(false);
            _elements.SmallRoot.SetActive(true);

            HideAll(_elements.Small);

            var additional = resultMax - baseMax;
            var spent = resultMax - current;

            var small = _elements.Small;

            for (var i = 0; i < resultMax; i++)
            {
                if (i >= small.Length)
                    break;

                var entry = small[i];
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

        /// <summary>
        /// Всё, что ряд берёт из сгенерированных биндингов: корни обоих рядов и их пипсы.
        /// Мелкие идут одним списком в порядке отрисовки — чётные в верхнюю строку, нечётные в нижнюю.
        /// </summary>
        public readonly struct Elements
        {
            public Elements(
                GameObject largeRoot,
                PlayerResourceEntry[] large,
                GameObject smallRoot,
                PlayerResourceEntry[] small)
            {
                LargeRoot = largeRoot;
                Large = large;
                SmallRoot = smallRoot;
                Small = small;
            }

            public GameObject LargeRoot { get; }
            public PlayerResourceEntry[] Large { get; }
            public GameObject SmallRoot { get; }
            public PlayerResourceEntry[] Small { get; }
        }
    }
}
