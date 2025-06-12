using Global.UI;
using UnityEngine;

namespace Menu.Shop
{
    [DisallowMultipleComponent]
    public class MenuShop : MonoBehaviour, IMenuShop
    {
        public IUIConstraints Constraints => UIConstraints.Empty;
    }
}