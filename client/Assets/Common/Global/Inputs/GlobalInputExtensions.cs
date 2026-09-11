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

            return builder;
        }
    }
}