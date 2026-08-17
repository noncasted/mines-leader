using Internal;
using Meta;
using TMPro;
using UnityEngine;
using VContainer;

namespace GamePlay.Players
{
    [DisallowMultipleComponent]
    public class AvatarView : MonoBehaviour, IScopeSetup, IEntityComponent
    {
        [SerializeField] private SpriteRenderer _avatarSprite;
        [SerializeField] private TMP_Text _healthText;
        [SerializeField] private TMP_Text _manaText;
        [SerializeField] private AvatarMovesView _movesView;

        private IPlayerMana _mana;
        private IPlayerHealth _health;
        private CharacterAvatars _avatars;
        private IGamePlayerInfo _info;

        [Inject]
        internal void Construct(
            IPlayerMana mana,
            IPlayerHealth health,
            IGamePlayerInfo info,
            CharacterAvatars avatars)
        {
            _info = info;
            _avatars = avatars;
            _health = health;
            _mana = mana;
        }

        public void Register(IEntityBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IScopeSetup>()
                   .AsSelfResolvable();

            builder
                .RegisterComponent(_movesView)
                .As<IScopeLoaded>()
                .AsSelfResolvable();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _health.Current.View(lifetime, value => _healthText.text = value.ToString());
            _mana.Current.View(lifetime, value => _manaText.text = value.ToString());
            _avatarSprite.sprite = _avatars.Value[_info.SelectedCharacter];
        }
    }
}