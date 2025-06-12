using UnityEngine;

namespace GamePlay.Services
{
    public interface IGameCamera
    {
        Camera Camera { get; }

        void SetSize(float size, float time);
    }
}