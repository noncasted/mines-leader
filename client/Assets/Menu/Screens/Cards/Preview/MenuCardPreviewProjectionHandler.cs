using Internal;
using Meta;
using Shared;
using UnityEngine;

namespace Menu.Screens.Cards.Preview
{
    /// <summary>
    /// Listens to the backend-delivered <see cref="InitialCardPreviews"/> projection
    /// and pushes the payload into the menu card preview cache.
    /// </summary>
    public sealed class MenuCardPreviewProjectionHandler : IScopeSetup
    {
        public MenuCardPreviewProjectionHandler(
            IBackendProjection<InitialCardPreviews> projection,
            IMenuCardPreviewCache cache)
        {
            _projection = projection;
            _cache = cache;
        }

        private readonly IBackendProjection<InitialCardPreviews> _projection;
        private readonly IMenuCardPreviewCache _cache;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _projection.Listen(lifetime, value =>
            {
                if (value?.Bundles == null)
                    return;

                _cache.Set(value.Bundles);
            });
        }
    }
}
