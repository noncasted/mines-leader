using Internal;
using UnityEngine;

namespace Global.Cameras
{
    public static class GlobalCameraExtensions
    {
        public static IScopeBuilder AddCamera(this IScopeBuilder builder)
        {
            builder.Register<CurrentCamera>()
                   .As<ICurrentCamera>();

            var camera = builder.Instantiate(Prefabs.Global.GlobalCamera, new Vector3(0f, 0f, -10f));
            camera.gameObject.SetActive(false);

            builder.RegisterComponent(camera)
                   .As<IGlobalCamera>()
                   .As<IScopeBaseSetup>();

            builder.Register<CameraUtils>()
                   .As<ICameraUtils>();

            return builder;
        }
    }
}
