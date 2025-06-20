using Internal;
using TMPro;
using UnityEngine;

namespace GamePlay.Boards
{
    [DisallowMultipleComponent]
    public class CellFreeView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _count;

        public void Enable(IReadOnlyLifetime lifetime, ICellFreeState state)
        {
            gameObject.SetActive(true);
            _count.gameObject.SetActive(false);
            lifetime.Listen(() => gameObject.SetActive(false));

            state.MinesAround.Advise(lifetime, count =>
            {
                if (count == 0)
                {
                    _count.gameObject.SetActive(false);
                    return;
                }

                _count.gameObject.SetActive(true);
                _count.text = count.ToString();
            });
        }
    }
}