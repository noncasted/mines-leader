using Internal;
using Shared;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Assets.Meta
{
    [InlineEditor]
    public class CardDefinition : EnvAsset, IEnvDictionaryKeyProvider<CardType>, ICardDefinition
    {
        [SerializeField] private CardType _type;
        [SerializeField] private CardTarget _target;
        [SerializeField] private int _manaCost;

        [SerializeField] private string _name;
        [SerializeField] [Multiline] private string _description;

        [SerializeField] [PreviewField(300, FilterMode = FilterMode.Point)]
        private Sprite _image;

        public CardType Type => _type;
        public CardTarget Target => _target;
        public int ManaCost => _manaCost;
        public string Name => _name;
        public string Description => _description;
        public Sprite Image => _image;

        public CardType EnvKey => Type;
    }
}