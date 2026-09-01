using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;
using UnityEngine.UI;

namespace GamePlay.UI
{
    [DisallowMultipleComponent]
    public class GamePauseLeave : MonoBehaviour
    {
        [SerializeField] private Button _acceptButton;
        [SerializeField] private Button _cancelButton;

        public async UniTask<bool> Process(IReadOnlyLifetime lifetime)
        {
            gameObject.SetActive(true);

            var menuLifetime = lifetime.Child();
            var completion = new UniTaskCompletionSource<bool>();

            _acceptButton.ListenClick(menuLifetime, () => completion.TrySetResult(true));
            _cancelButton.ListenClick(menuLifetime, () => completion.TrySetResult(false));

            var result = await completion.Task;

            menuLifetime.Terminate();
            gameObject.SetActive(false);

            return result;
        }
    }
}