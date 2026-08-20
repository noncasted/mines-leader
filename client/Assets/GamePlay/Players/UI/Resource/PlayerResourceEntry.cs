using UnityEngine;
using UnityEngine.UI;

namespace GamePlay.Players.Resource
{
    [DisallowMultipleComponent]
    public class PlayerResourceEntry : MonoBehaviour
    {
        [SerializeField] private Image _image;

        public void SetFull(PlayerResourceOptions options, bool isLarge, bool isBase)
        {
            if (isBase == true)
                _image.sprite = isLarge ? options.LargeBaseFull : options.SmallBaseFull;
            else
                _image.sprite = isLarge ? options.LargeAdditionalFull : options.SmallAdditionalFull;
        }

        public void SetEmpty(PlayerResourceOptions options, bool isLarge, bool isBase)
        {
            if (isBase == true)
                _image.sprite = isLarge ? options.LargeBaseEmpty : options.SmallBaseEmpty;
            else
                _image.sprite = isLarge ? options.LargeAdditionalEmpty : options.SmallAdditionalEmpty;
        }
    }
}