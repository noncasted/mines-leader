using Internal;
using UnityEngine;

namespace GamePlay.Boards
{
    [DisallowMultipleComponent]
    public class CellTakenView : MonoBehaviour
    {
        [SerializeField] private GameObject _mine;
        [SerializeField] private GameObject _flag;
        
        public void Enable(IReadOnlyLifetime lifetime, ICellTakenState state)
        {
            gameObject.SetActive(true);
            _mine.SetActive(false);
            state.IsFlagged.Advise(lifetime, isFlagged => _flag.SetActive(isFlagged));
            lifetime.Listen(() => gameObject.SetActive(false));
        }
        
        public void OnExplosion()
        {
            gameObject.SetActive(false);
            _mine.SetActive(true);
        }
    }
}