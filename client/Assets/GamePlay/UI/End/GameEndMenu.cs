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
        [SerializeField] private DesignButton _completeButton;
        
        public UniTask Show(IReadOnlyLifetime lifetime, GameResult result)
        {
            gameObject.SetActive(true);
            return _completeButton.WaitClick(lifetime);
        }
    }
}