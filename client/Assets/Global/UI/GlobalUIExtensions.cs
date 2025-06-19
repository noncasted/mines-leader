using Internal;

namespace Global.UI
{
    public static class GlobalUIExtensions
    {
        public static void AddUI(this IScopeBuilder builder)
        {
            builder.Register<UIStateMachine>()
                .WithScopeLifetime()
                .As<IUIStateMachine>();
        }
    }
}