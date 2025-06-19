using UnityEngine;

namespace Global.Cameras
{
    public interface ICameraUtils
    {
        Vector3 ScreenToWorld(Vector3 screen);
    }
    
    public class CameraUtils : ICameraUtils
    {
        public CameraUtils(ICurrentCamera camera)
        {
            _camera = camera;
        }

        private readonly ICurrentCamera _camera;

        public Vector3 ScreenToWorld(Vector3 screen)
        {
            if (_camera.Current == null)
                return Vector3.zero;

            return _camera.Current.ScreenToWorldPoint(screen);
        }
    }
}