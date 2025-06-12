using Cysharp.Threading.Tasks;
using Internal;

namespace Global.UI
{
    public static class GlobalUIExtensions
    {
        public static async UniTask AddUI(this IScopeBuilder builder)
        {
            builder.Register<UIStateMachine>()
                .WithScopeLifetime()
                .As<IUIStateMachine>();
        }
    }
}