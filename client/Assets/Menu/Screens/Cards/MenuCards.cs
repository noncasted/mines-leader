using Global.UI;
using UnityEngine;

namespace Menu.Screens
{
    [DisallowMultipleComponent]
    public class MenuCards : MonoBehaviour, IMenuCards
    {
        public IUIConstraints Constraints => UIConstraints.Empty;
    }
}