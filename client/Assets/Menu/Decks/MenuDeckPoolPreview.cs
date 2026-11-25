using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace Menu.Decks
{
    // The animated preview of a card of the pool next to the hovered spot.
    public class MenuDeckPoolPreview
    {
        public MenuDeckPoolPreview(
            IMenuCardPreviewPlayer previewPlayer,
            MenuCardPreviewPopup popup,
            RectTransform popupRect,
            RectTransform canvasRect)
        {
            _previewPlayer = previewPlayer;
            _previewPopup = popup;
            _previewPopupRect = popupRect;
            _canvasRect = canvasRect;
        }

        private readonly IMenuCardPreviewPlayer _previewPlayer;
        private readonly MenuCardPreviewPopup _previewPopup;
        private readonly RectTransform _previewPopupRect;
        private readonly RectTransform _canvasRect;

        public void RegisterHover(MenuDeckPoolSpot spot, IReadOnlyLifetime lifetime)
        {
            return;
            spot.PointerHandler.IsHovered.Advise(lifetime, isHovered => {
                if (spot.Card.gameObject.activeInHierarchy == false)
                    return;

                if (isHovered == false)
                    return;

                var hoverLifetime = spot.PointerHandler.IsHovered.ValueLifetime;
                ShowPreview(hoverLifetime, spot);
            });
        }

        private void ShowPreview(IReadOnlyLifetime lifetime, MenuDeckPoolSpot spot)
        {
            var card = spot.Card;
            var type = card.CardDefinition.Type;

            if (_previewPlayer.HasPreview(type) == false)
                return;

            _previewPlayer.Play(lifetime, type).Forget();
            var rt = _previewPlayer.PreviewTexture;

            _previewPopup.Show(rt);
            PositionPreview(spot);

            lifetime.Listen(() => _previewPopup.Hide());
        }

        private void PositionPreview(MenuDeckPoolSpot spot)
        {
            var spotRect = spot.Transform;
            var popupRect = _previewPopupRect;

            Vector3[] corners = new Vector3[4];
            spotRect.GetWorldCorners(corners);

            var worldRightCenter = (corners[2] + corners[3]) * 0.5f;

            var canvasScale = _canvasRect.localScale.x;

            worldRightCenter.x += 100f / canvasScale;

            var targetLocal = popupRect.parent.InverseTransformPoint(worldRightCenter);

            var size = popupRect.sizeDelta;
            var popupScale = popupRect.localScale;
            var pivot = popupRect.pivot;

            var centerOffset = new Vector3(
                (0.5f - pivot.x) * size.x * popupScale.x,
                (0.5f - pivot.y) * size.y * popupScale.y,
                0f);

            popupRect.localPosition = targetLocal - centerOffset;
        }
    }
}
