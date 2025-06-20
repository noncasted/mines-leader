using Global.UI;
using UnityEngine;

namespace Menu.Screens
{
    [DisallowMultipleComponent]
    public class MenuShop : MonoBehaviour, IMenuShop
    {
        public IUIConstraints Constraints => UIConstraints.Empty;
    }
}