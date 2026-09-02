using Shared;
using TMPro;
using UnityEngine;

namespace GamePlay.UI
{
    [DisallowMultipleComponent]
    public class GameResultsStats : MonoBehaviour
    {
        [SerializeField] private TMP_Text _flags;
        [SerializeField] private TMP_Text _cards;
        [SerializeField] private TMP_Text _reveal;
        [SerializeField] private TMP_Text _attack;

        public void Show(IUserStatsState stats)
        {
            _flags.text = Format("flags", stats, UserStatType.FlagsSet);
            _cards.text = Format("cards", stats, UserStatType.CardsPlayed);
            _reveal.text = Format("reveal", stats, UserStatType.CellsOpened);
            _attack.text = Format("attack", stats, UserStatType.EnemyCellsPlanted);
        }

        private static string Format(string label, IUserStatsState stats, UserStatType type)
        {
            var amount = stats?.Get(type) ?? 0;

            return $"{label}: {amount}";
        }
    }
}
