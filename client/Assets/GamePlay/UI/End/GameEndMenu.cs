using Cysharp.Threading.Tasks;
using GamePlay.Loop;
using Global.UI;
using Internal;
using UnityEngine;

namespace GamePlay.UI
{
    public interface IGameEndMenu
    {
        UniTask Show(IReadOnlyLifetime lifetime, GameResult result);
    }

    [DisallowMultipleComponent]
    public class GameEndMenu : MonoBehaviour, IGameEndMenu
    {
        [SerializeField] private GameEndRating _rating;
        
        [SerializeField] private DesignButton _menuButton;
        [SerializeField] private DesignButton _rematchButton;
        
        public UniTask Show(IReadOnlyLifetime lifetime, GameResult result)
        {
            _rating.Show(result.CurrentRating, result.RatingChange);
            gameObject.SetActive(true);
            return _menuButton.WaitClick(lifetime);
        }
    }
}