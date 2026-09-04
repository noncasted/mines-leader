using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;
using UnityEngine.UI;

namespace GamePlay.UI
{
    public interface IGamePauseLeave
    {
        /// <summary>
        /// Показывает подтверждение выхода и ждёт ответа игрока.
        /// </summary>
        UniTask<bool> Process(IReadOnlyLifetime lifetime);
    }

    public class GamePauseLeave : IGamePauseLeave
    {
        public GamePauseLeave(GamePauseLeaveAttentionBindings bindings)
        {
            var buttons = bindings.Plate.Buttons;
            _acceptButton = buttons.Yes.Button;
            _cancelButton = buttons.No.Button;
            _gameObject = bindings.GameObject;

            _gameObject.SetActive(false);
        }

        private readonly Button _acceptButton;
        private readonly Button _cancelButton;
        private readonly GameObject _gameObject;

        public async UniTask<bool> Process(IReadOnlyLifetime lifetime)
        {
            _gameObject.SetActive(true);

            var menuLifetime = lifetime.Child();
            var completion = new UniTaskCompletionSource<bool>();

            _acceptButton.ListenClick(menuLifetime, () => completion.TrySetResult(true));
            _cancelButton.ListenClick(menuLifetime, () => completion.TrySetResult(false));

            var result = await completion.Task;

            menuLifetime.Terminate();
            _gameObject.SetActive(false);

            return result;
        }
    }
}
