using System;
using Internal;
using Meta;
using TMPro;
using Tools;
using UnityEngine;
using UnityEngine.UI;

namespace Menu.Unlocks
{
    [DisallowMultipleComponent]
    public class MenuUnlockEntry : MonoBehaviour
    {
        [SerializeField] private GameObject _lock;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _counter;
        [SerializeField] private Image _background;
        [SerializeField] private Button _button;

        private AchievementStatus _status = AchievementStatus.Locked;

        public IAchievementTier Tier { get; private set; }
        private Button Button => _button;

        public void Setup(IAchievementRow row, IAchievementTier tier)
        {
            Tier = tier;

            _name.text = string.IsNullOrEmpty(tier.Description)
                ? $"{row.Name} {tier.Tier}"
                : tier.Description;

            ApplyStatus(AchievementStatus.Locked);
        }

        /// <summary>
        /// Подписывает вид на прогресс, статус и клик. Вызывается при входе на экран,
        /// сам вид живёт дольше — он собирается один раз при старте сцены.
        /// </summary>
        public void Bind(IReadOnlyLifetime lifetime, Action<MenuUnlockEntry> clicked)
        {
            Tier.Progress.View(lifetime, progress => UpdateCounter(progress, Tier.Target));
            Tier.Status.View(lifetime, ApplyStatus);

            if (Button == null)
            {
                Debug.LogError($"[MenuUnlockEntry] {name} has no Button, unlock is not clickable");
                return;
            }

            Button.ListenClick(lifetime, () => {
                if (_status != AchievementStatus.Available)
                    return;

                clicked.Invoke(this);
            });
        }

        private void UpdateCounter(long progress, long target)
        {
            var clamped = target > 0 && progress > target ? target : progress;
            _counter.text = $"{clamped}/{target}";
        }

        private void ApplyStatus(AchievementStatus status)
        {
            _status = status;

            switch (status)
            {
                case AchievementStatus.Taken:
                    _background.sprite = Sprites.MenuUnlocks.Taken;
                    _lock.SetActive(false);
                    _name.gameObject.SetActive(true);
                    _counter.gameObject.SetActive(true);
                    break;
                case AchievementStatus.InProgress:
                    _background.sprite = Sprites.MenuUnlocks.InProgress;
                    _lock.SetActive(false);
                    _name.gameObject.SetActive(true);
                    _counter.gameObject.SetActive(true);
                    break;
                case AchievementStatus.Available:
                    _background.sprite = Sprites.MenuUnlocks.Available;
                    _lock.SetActive(false);
                    _name.gameObject.SetActive(true);
                    _counter.gameObject.SetActive(true);
                    break;
                case AchievementStatus.Locked:
                    _background.sprite = Sprites.MenuUnlocks.Locked;
                    _lock.SetActive(true);
                    _name.gameObject.SetActive(false);
                    _counter.gameObject.SetActive(false);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }

            Button.interactable = status == AchievementStatus.Available;
        }
    }
}