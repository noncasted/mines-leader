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
            
            _board.Updated.Advise(lifetime, () =>
            {
                var mines = 0;
                
                foreach (var (_, cell) in _board.Cells)
                {
                    if (cell.State.Value.Status != CellStatus.Taken)
                        continue;

                    var state = cell.EnsureTaken();
                    
                    if (state.HasMine.Value == true)
                        mines++;
                }
                
                if (mines != 0)
                    _gameStarted = true;
                
                if (_gameStarted == false)
                    return;

                _text.text = mines.ToString();
            });
        }
    }
}