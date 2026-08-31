using Meta;
using Shared;
using TMPro;
using Internal;
using UnityEngine;
using UnityEngine.UI;

namespace Menu.Profile
{
    /// <summary>
    /// Карта в раскладке матча. Только показ: ни драга, ни выбора здесь нет,
    /// поэтому данные приходят готовыми и живут до пересборки раскладки.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuProfileCard : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private Image _outline;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private TMP_Text _manaCost;

        public void Setup(ICardDefinition definition, string description, int manaCost)
        {
            _image.sprite = definition.Image;
            _name.text = definition.Name;
            _description.text = description;
            _manaCost.text = manaCost.ToString();

            _outline.color = definition.Group switch
            {
                CardGroup.Scout => Colors.Deck.Scout,
                CardGroup.Defense => Colors.Deck.Defense,
                CardGroup.Attack => Colors.Deck.Attack,
                CardGroup.Buff => Colors.Deck.Buff,
                CardGroup.Debuff => Colors.Deck.Debuff,
                _ => Color.black
            };
        }
    }
}
