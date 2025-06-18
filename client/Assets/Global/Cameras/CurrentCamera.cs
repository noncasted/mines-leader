using UnityEngine;

namespace Global.Cameras
{
    public interface ICurrentCamera
    {
        Camera Current { get; }

        void SetCamera(Camera current);
    }
    
    public class CurrentCamera : ICurrentCamera
    {
        private Camera _current;

        public Camera Current => _current;

        public void SetCamera(Camera current)
        {
            _current = current;
        }
    }
}