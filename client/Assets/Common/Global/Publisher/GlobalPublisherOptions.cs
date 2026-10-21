using Internal;
using TMPro;
using UnityEngine;

namespace Global.Publisher
{
    public class GlobalPublisherOptions : EnvAsset
    {
        // The menu font for the "update available" notice (Global.UI.ClientOutdatedNotice): it is built from code and
        // may show before any menu asset is loaded.
        [SerializeField] private TMP_FontAsset _outdatedNoticeFont;

        public TMP_FontAsset OutdatedNoticeFont => _outdatedNoticeFont;
    }
}