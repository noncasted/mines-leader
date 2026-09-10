using Internal;
using UnityEngine.EventSystems;

namespace Global.Inputs
{
    public static class GlobalInputExtensions
    {
        public static IScopeBuilder AddInput(this IScopeBuilder builder)
        {
            builder.Register<InputConstraintsStorage>()
                   .As<IInputConstraintsStorage>();

            var eventSystemPrefab = GlobalPrefabs.GlobalEvents.GetComponent<EventSystem>();
            builder.Instantiate(eventSystemPrefab);

            builder.Register<GlobalControls>()
                   .As<IGlobalControls>()
                   .As<IScopeSetup>();

            return builder;
        }
    }
}