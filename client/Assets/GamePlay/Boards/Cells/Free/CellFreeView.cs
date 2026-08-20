using Internal;
using TMPro;
using Tools;
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

            state.MinesAround.Advise(lifetime, count => {
                if (count == 0)
                {
                    _count.gameObject.SetActive(false);
                    return;
                }

                _count.gameObject.SetActive(true);
                _count.text = count.ToString();
return;
                _count.color = count switch
                {
                    1 => Colors.MinesAround.C1,
                    2 => Colors.MinesAround.C2,
                    3 => Colors.MinesAround.C3,
                    4 => Colors.MinesAround.C4,
                    5 => Colors.MinesAround.C5,
                    6 => Colors.MinesAround.C6,
                    7 => Colors.MinesAround.C7,
                    8 => Colors.MinesAround.C8,
                    _ => Color.black
                };
            });
        }
    }
}