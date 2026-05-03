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
            return _bundles.TryGetValue(cardType, out bundle);
        }

        public void Set(IReadOnlyList<CardPreviewBundle> bundles)
        {
            _bundles.Clear();

            foreach (var bundle in bundles)
                _bundles[bundle.CardType] = bundle;
        }

        public void Clear()
        {
            _bundles.Clear();
        }
    }
}
