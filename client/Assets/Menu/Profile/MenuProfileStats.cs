using Internal;
using Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Menu.Profile
{
    /// <summary>Шапка профиля: аватар, имя и три счётчика с сервера.</summary>
    [DisallowMultipleComponent]
    public class MenuProfileStats : MonoBehaviour
    {
        [SerializeField] private Image _avatar;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _loses;
        [SerializeField] private TMP_Text _wins;
        [SerializeField] private TMP_Text _rating;

        public void Bind(IReadOnlyLifetime lifetime, IProfile profile)
        {
            profile.Name.View(lifetime, value => _name.text = value);
            profile.Loses.View(lifetime, value => _loses.text = value.ToString());
            profile.Wins.View(lifetime, value => _wins.text = value.ToString());
            profile.Rating.View(lifetime, value => _rating.text = value.ToString());
        }

        public void SetAvatar(Sprite sprite)
        {
            if (_avatar == null || sprite == null)
                return;

            _avatar.sprite = sprite;
        }
    }
}
