using Cysharp.Threading.Tasks;
using Internal;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GamePlay.UI
{
    public interface IGameFailureNotice
    {
        // Tells the player the match ended with an error and waits until the player goes back to the menu.
        UniTask Show(IReadOnlyLifetime lifetime);
    }

    // The match has no window of its own for an error: the notice takes the attention plate of the pause (the leave
    // confirmation) with its own text and one button. Nothing restores the plate: the match scope ends after the notice.
    public class GameFailureNotice : IGameFailureNotice
    {
        public GameFailureNotice(GamePauseLeaveAttentionBindings bindings)
        {
            var plate = bindings.Plate;
            var buttons = plate.Buttons;

            _gameObject = bindings.GameObject;
            _text = plate.Text.TextMeshProUGUI;
            _menuButton = buttons.Yes.Button;
            _menuButtonText = buttons.Yes.TextTMP.TextMeshProUGUI;
            _cancelButton = buttons.No.GameObject;
        }

        private const string Message = "Match error: the game lost sync with the server";
        private const string MenuButtonText = "Menu";

        private readonly GameObject _gameObject;
        private readonly TextMeshProUGUI _text;
        private readonly Button _menuButton;
        private readonly TextMeshProUGUI _menuButtonText;
        private readonly GameObject _cancelButton;

        public async UniTask Show(IReadOnlyLifetime lifetime)
        {
            _text.text = Message;
            _menuButtonText.text = MenuButtonText;

            // Hidden before the plate is shown: the layout of the buttons is rebuilt on enable, the one left is centred.
            _cancelButton.SetActive(false);
            _gameObject.SetActive(true);

            var completion = new UniTaskCompletionSource();
            _menuButton.ListenClick(lifetime, () => completion.TrySetResult());

            await completion.Task.AttachExternalCancellation(lifetime.Token);
        }
    }
}
