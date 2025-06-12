using Internal;
using UnityEngine;

namespace GamePlay.Boards.Taken
{
    [DisallowMultipleComponent]
    public class CellTakenView : MonoBehaviour
    {
        [SerializeField] private GameObject _mine;
        [SerializeField] private GameObject _flag;
        
        public void Enable(IReadOnlyLifetime lifetime, ICellTakenState state)
        {
            gameObject.SetActive(true);
            state.IsFlagged.Advise(lifetime, isFlagged => _flag.SetActive(isFlagged));
            lifetime.Listen(() => gameObject.SetActive(false));
        }
        
        public void RevealMine()
        {
            gameObject.SetActive(false);
            _mine.SetActive(true);
        }
    }
}