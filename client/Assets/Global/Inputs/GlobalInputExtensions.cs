using Internal;
using Tools;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Global.Inputs
{
    public static class GlobalInputExtensions
    {
        public static IScopeBuilder AddInput(this IScopeBuilder builder)
        {
            builder.Register<InputConstraintsStorage>()
                   .As<IInputConstraintsStorage>();

            var eventSystemPrefab = Prefabs.GlobalEvents.As<EventSystem>();
            builder.Instantiate(eventSystemPrefab);

            builder.Register<GlobalControls>()
                   .As<IGlobalControls>()
                   .As<IScopeSetup>();

            return builder;
        }
    }

    [PrefabDefinition]
    public static class GlobalEventSystemPrefab
    {
        public static void Define(PrefabBuilder builder)
        {
            builder
                .WithName("Global/Global_Events")
                .WithComponent<EventSystem>()
                .WithComponent<InputSystemUIInputModule>();
        }
    }
}