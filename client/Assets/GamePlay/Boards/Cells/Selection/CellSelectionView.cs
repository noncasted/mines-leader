using UnityEngine;

namespace GamePlay.Boards
{
    public interface ICellSelectionView
    {
        void Select();
        void Deselect();
    }
    
    [DisallowMultipleComponent]
    public class CellSelectionView : MonoBehaviour, ICellSelectionView
    {
        public void Select()
        {
            gameObject.SetActive(true);
        }

        public void Deselect()
        {
            gameObject.SetActive(false);
        }
    }
}