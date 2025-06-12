using Global.UI;
using UnityEngine;

namespace Menu.Settings
{
    [DisallowMultipleComponent]
    public class MenuSettings : MonoBehaviour, IMenuSettings
    {
        public IUIConstraints Constraints => UIConstraints.Empty;
    }
}