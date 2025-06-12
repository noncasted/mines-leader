using Internal;

namespace Menu.Loop
{
    public static class MenuLoopExtensions
    {
        public static IScopeBuilder AddMenuLoop(this IScopeBuilder builder)
        {
            builder.Register<MenuLoop>()
                .As<IMenuLoop>();

            return builder;
        } 
    }
}