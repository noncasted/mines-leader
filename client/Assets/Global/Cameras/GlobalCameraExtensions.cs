using Internal;
using Tools.PrefabBuilder;
using UnityEngine;

namespace Global.Cameras
{
    public static class GlobalCameraExtensions
    {
        public static IScopeBuilder AddCamera(this IScopeBuilder builder)
        {
            builder.Register<CurrentCamera>()
                   .As<ICurrentCamera>();

            var camera = builder.Instantiate(Prefabs.GlobalCamera.As<GlobalCamera>(), new Vector3(0f, 0f, -10f));
            camera.gameObject.SetActive(false);

            builder.RegisterComponent(camera)
                   .As<IGlobalCamera>()
                   .AsEventListener<IScopeBaseSetup>();

            builder.Register<CameraUtils>()
                   .As<ICameraUtils>();

            return builder;
        }
    }

    [PrefabDefinition]
    public static class GlobalCameraPrefab
    {
        public static void Define(PrefabBuilder builder)
        {
            builder
                .WithName("Global/Global_Camera")
                .WithComponent<Camera>(camera => {
                            camera.orthographic = true;
                            camera.orthographicSize = 3.1f;
                            camera.nearClipPlane = 0.3f;
                            camera.farClipPlane = 1000f;
                            camera.clearFlags = CameraClearFlags.SolidColor;
                            camera.backgroundColor = Color.black;
                            camera.useOcclusionCulling = true;
                        }
                    )
                .WithComponent<GlobalCamera>();
        }
    }
}