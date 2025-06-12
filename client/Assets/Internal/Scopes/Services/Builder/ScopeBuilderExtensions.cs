using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Internal
{
    public static class ScopeBuilderExtensions
    {
        public static UniTask<TComponent> FindOrLoadScene<TScene, TComponent>(this IScopeBuilder builder)
            where TScene : SceneData
            where TComponent : MonoBehaviour
        {
            var scene = builder.GetAsset<TScene>();
            return builder.FindOrLoadScene<TComponent>(scene);
        }

        public static async UniTask FindOrLoadSceneWithServices<TScene>(this IScopeBuilder builder, bool isMain = false)
            where TScene : SceneData
        {
            var scene = builder.GetAsset<TScene>();
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

        public static IScopeBuilder AddFromFactory<T>(this IScopeBuilder builder) where T : ServiceFactoryBase
        {
            return builder.GetAsset<T>().Process(builder);
        }

        public static UniTask<IScopeBuilder> AddFromAsyncFactory<T>(this IScopeBuilder builder)
            where T : ServiceFactoryBaseAsync
        {
            return builder.GetAsset<T>().Process(builder);
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