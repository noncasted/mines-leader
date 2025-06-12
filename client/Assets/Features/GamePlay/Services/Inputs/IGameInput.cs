using Internal;
using UnityEngine;

namespace GamePlay.Services
{
    public interface IGameInput
    {
        IViewableProperty<bool> Flag { get; }
        IViewableProperty<bool> Open { get; }
        
        Vector2 World { get; }
        Vector2 Screen { get; }
    }
}