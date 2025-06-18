using Internal;

namespace Menu.Common
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