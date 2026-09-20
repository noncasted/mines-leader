using Internal;
using Meta;
using TMPro;
using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardRevealView
    {
        void Reveal();
    }

    // Удалённая карта лежит рубашкой вверх: лицо на префабе выключено, а вскрытие просто
    // меняет, какая из двух сторон активна.
    public class CardRevealView : IScopeSetup, ICardRevealView
    {
        public CardRevealView(
            GameCardBindings bindings,
            ICardDefinition definition,
            ICardDescriptionProvider descriptionProvider)
        {
            var view = bindings.View;
            var body = view.Body;

            _back = view.Back.GameObject;
            _front = body.GameObject;
            _image = body.Image.SpriteRenderer;
            _name = body.Name.TextMeshPro;
            _description = body.Description.TextMeshPro;

            _definition = definition;
            _descriptionProvider = descriptionProvider;
        }

        private readonly GameObject _back;
        private readonly GameObject _front;
        private readonly SpriteRenderer _image;
        private readonly TMP_Text _name;
        private readonly TMP_Text _description;

        private readonly ICardDefinition _definition;
        private readonly ICardDescriptionProvider _descriptionProvider;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _image.sprite = _definition.Image;
            _name.text = _definition.Name;
            _description.text = _descriptionProvider.GetDescription(_definition.Type);
        }

        public void Reveal()
        {
            _back.SetActive(false);
            _front.SetActive(true);
        }
    }
}
