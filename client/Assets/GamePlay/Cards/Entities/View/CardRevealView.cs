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

    [DisallowMultipleComponent]
    public class CardRevealView : MonoBehaviour, IEntityComponent, IScopeSetup, ICardRevealView
    {
        [SerializeField] private GameObject _back;
        [SerializeField] private GameObject _front;
        [SerializeField] private SpriteRenderer _image;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _description;

        private ICardDefinition _definition;
        private ICardDescriptionProvider _descriptionProvider;

        [Inject]
        internal void Construct(ICardDefinition definition, ICardDescriptionProvider descriptionProvider)
        {
            _definition = definition;
            _descriptionProvider = descriptionProvider;
        }

        public void Register(IEntityBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IScopeSetup>()
                   .As<ICardRevealView>();
        }

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
