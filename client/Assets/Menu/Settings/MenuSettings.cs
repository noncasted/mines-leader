using Global.UI;
using UnityEngine;

namespace Menu.Settings
{
    public interface IMenuSettings : IUIState
    {
    }

    [DisallowMultipleComponent]
    public class MenuSettings : MonoBehaviour, IMenuSettings
    {
        public IUIConstraints Constraints => UIConstraints.Empty;
    }
}