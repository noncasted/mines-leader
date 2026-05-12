using Cysharp.Threading.Tasks;
using Global.UI;
using Internal;
using UnityEngine;

namespace GamePlay.UI
{
    [DisallowMultipleComponent]
    public class PauseLeaveMenu : MonoBehaviour
    {
        [SerializeField] private DesignButton _acceptButton;
        [SerializeField] private DesignButton _cancelButton;

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