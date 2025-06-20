using Global.UI;
using UnityEngine;

namespace Menu.Screens
{
    public interface IMenuShop : IUIState
    {
        
    }
    
    [DisallowMultipleComponent]
    public class MenuShop : MonoBehaviour, IMenuShop
    {
        public IUIConstraints Constraints => UIConstraints.Empty;
    }
}