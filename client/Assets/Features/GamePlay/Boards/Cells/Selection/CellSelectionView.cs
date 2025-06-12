using UnityEngine;

namespace GamePlay.Boards
{
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