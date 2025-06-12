using Global.UI;
using UnityEngine;

namespace Menu.Cards
{
    [DisallowMultipleComponent]
    public class MenuCards : MonoBehaviour, IMenuCards
    {
        public IUIConstraints Constraints => UIConstraints.Empty;
    }
}