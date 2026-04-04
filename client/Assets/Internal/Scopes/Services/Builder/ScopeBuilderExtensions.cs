using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace Internal
{
    public static class ScopeBuilderExtensions
    {
        public static async UniTask FindOrLoadSceneWithServices(
            this IScopeBuilder builder,
            AssetReference scene,
            bool isMain = false)
        {
            var services = await builder.FindOrLoadScene<SceneServicesFactory>(scene, isMain);
            services.Create(builder);
        }

        public static IRegistration RegisterScriptableRegistry<T1, T2>(this IScopeBuilder builder)
            where T1 : ScriptableRegistry<T2>
            where T2 : EnvAsset
        {
            var registry = builder.GetAsset<T1>();
            registry.Initialize();
            var registration = builder.RegisterInstance(registry);
            registration.As<IScriptableRegistry<T2>>();
            return registration;
        }

        public static T Instantiate<T>(this IScopeBuilder builder, T prefab) where T : MonoBehaviour
        {
            var instance = Object.Instantiate(prefab);
            instance.name = prefab.name;
            builder.Binder.MoveToModules(instance);

            return instance;
        }

        public static T Instantiate<T>(this IScopeBuilder builder, T prefab, Vector3 position) where T : MonoBehaviour
        {
            var instance = Object.Instantiate(prefab, position, Quaternion.identity);
            instance.name = prefab.name;
            builder.Binder.MoveToModules(instance);

            return instance;
        }
    }
}