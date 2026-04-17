using System.Collections.Generic;
using Shared;
using UnityEngine;

namespace Menu.Screens.Cards.Preview
{
    public interface IMenuCardPreviewCache
    {
        bool TryGet(CardType cardType, out CardPreviewBundle bundle);
        void Set(IReadOnlyList<CardPreviewBundle> bundles);
        void Clear();
    }

    public sealed class MenuCardPreviewCache : IMenuCardPreviewCache
    {
        private readonly Dictionary<CardType, CardPreviewBundle> _bundles = new();

        public bool TryGet(CardType cardType, out CardPreviewBundle bundle)
        {
            var found = _bundles.TryGetValue(cardType, out bundle);
            Debug.Log($"[Preview] Cache.TryGet({cardType}) = {found} (cache size={_bundles.Count}).");
            return found;
        }

        public void Set(IReadOnlyList<CardPreviewBundle> bundles)
        {
            _bundles.Clear();

            foreach (var bundle in bundles)
                _bundles[bundle.CardType] = bundle;

            Debug.Log($"[Preview] Cache.Set stored {_bundles.Count} bundles: [{string.Join(", ", _bundles.Keys)}].");
        }

        public void Clear()
        {
            Debug.Log("[Preview] Cache.Clear.");
            _bundles.Clear();
        }
    }
}
