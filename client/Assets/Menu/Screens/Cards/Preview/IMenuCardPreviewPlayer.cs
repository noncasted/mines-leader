using Shared;
using UnityEngine;

namespace Menu.Screens.Cards.Preview
{
    /// <summary>
    /// Контракт сервиса проигрывания превью карты на Menu_Board. Реализация — Agent A.
    /// Agent B (UI) потребляет только этот интерфейс: стартует воспроизведение на hover,
    /// останавливает на leave, читает RenderTexture доски для подстановки в popup.
    /// </summary>
    public interface IMenuCardPreviewPlayer
    {
        /// <summary>
        /// RenderTexture сцены Menu_Board. UI привязывает её к background-image popup.
        /// </summary>
        RenderTexture PreviewTexture { get; }

        /// <summary>
        /// True, если для этой карты на беке сгенерирован превью-бандл (поле-модифицирующие карты).
        /// Resource/buff/hand-карты без визуала возвращают false — UI по этому флагу скрывает popup.
        /// </summary>
        bool HasPreview(CardType cardType);

        /// <summary>
        /// Запускает сценарий превью для данной карты (сброс стейта + проигрывание снапшотов).
        /// </summary>
        void Play(CardType cardType);

        /// <summary>
        /// Останавливает воспроизведение и гасит картинку.
        /// </summary>
        void Stop();
    }
}
