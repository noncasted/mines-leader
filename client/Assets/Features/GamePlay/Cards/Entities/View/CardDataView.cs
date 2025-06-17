using Assets.Meta;
using Global.GameServices;
using Internal;
using TMPro;
using UnityEngine;
using VContainer;

namespace GamePlay.Cards
{
    [DisallowMultipleComponent]
    public class CardDataView : MonoBehaviour, IEntityComponent, IScopeSetup
    {
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _manaCost;
        [SerializeField] private SpriteRenderer _image;

        private ICardDefinition _definition;

        [Inject]
        private void Construct(ICardDefinition definition)
        {
            _definition = definition;
        }
        
        public void Register(IEntityBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _name.text = _definition.Name;
            _manaCost.text = _definition.ManaCost.ToString();
            _image.sprite = _definition.Image;
        }
    }
}