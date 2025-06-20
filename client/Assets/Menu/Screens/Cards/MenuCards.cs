using Global.UI;
using UnityEngine;

namespace Menu.Screens
{
    public interface IMenuCards : IUIState
    {
        
    }
    
    [DisallowMultipleComponent]
    public class MenuCards : MonoBehaviour, IMenuCards
    {
        public IUIConstraints Constraints => UIConstraints.Empty;
    }
}