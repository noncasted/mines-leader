using GamePlay.Boards;
using Internal;
using TMPro;
using UnityEngine;
using VContainer;

namespace GamePlay.Players
{
    [DisallowMultipleComponent]
    public class BoardMinesCounterView : MonoBehaviour, IEntityComponent, IScopeSetup
    {
        [SerializeField] private TMP_Text _text;

        private IBoard _board;
        private bool _gameStarted;

        [Inject]
        private void Construct(IBoard board)
        {
            _board = board;
        }

        public void Register(IEntityBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<IScopeSetup>();
        }

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _text.text = "?";

            _board.State.Advise(lifetime, state =>
                {
                    if (_gameStarted == false)
                    {
                        _gameStarted = true;
                        return;
                    }
                    
                    _text.text = (state.Mines - state.Flags).ToString();
                }
            );
        }
    }
}