using GamePlay.Boards;
using GamePlay.Loop;
using Internal;
using TMPro;
using UnityEngine;
using VContainer;

namespace GamePlay.UI
{
    [DisallowMultipleComponent]
    public class BoardMinesCounterView : MonoBehaviour, ISceneService, IGameStarted
    {
        [SerializeField] private TMP_Text _ownText;
        [SerializeField] private TMP_Text _opponentText;

        private IGameContext _context;

        [Inject]
        internal void Construct(IGameContext context)
        {
            _context = context;
        }

        public void Create(IScopeBuilder builder)
        {
            builder.RegisterComponent(this)
                   .As<IGameStarted>();
        }

        public void OnGameStarted(IReadOnlyLifetime lifetime)
        {
            _ownText.text = "?";
            _opponentText.text = "?";

            Bind(_context.Self.Board, _ownText);
            Bind(_context.Other.Board, _opponentText);

            return;

            void Bind(IBoard board, TMP_Text text)
            {
                board.State.Advise(lifetime, state => {
                    text.text = (state.Mines - state.Flags).ToString();
                });
            }
        }
    }
}