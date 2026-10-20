using Global.Publisher.Itch;
using Internal;
using TMPro;
using UnityEngine;

namespace Global.Publisher
{
    public class GlobalPublisherOptions : EnvAsset
    {
        [SerializeField] private ItchCallbacks _itchCallbacksPrefab;

        // The menu font for the "update available" notice (Global.UI.ClientOutdatedNotice): it is built from code and
        // may show before any menu asset is loaded.
        [SerializeField] private TMP_FontAsset _outdatedNoticeFont;

        public ItchCallbacks ItchCallbacksPrefab => _itchCallbacksPrefab;
        public TMP_FontAsset OutdatedNoticeFont => _outdatedNoticeFont;
    }
}