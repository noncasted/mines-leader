using Global.UI;
using UnityEngine;

namespace Menu.Screens
{
    [DisallowMultipleComponent]
    public class MenuSettings : MonoBehaviour, IMenuSettings
    {
        public IUIConstraints Constraints => UIConstraints.Empty;
    }
}