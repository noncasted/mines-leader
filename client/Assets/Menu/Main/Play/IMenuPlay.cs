using Internal;
using Meta;

namespace Menu.Main
{
    public interface IMenuPlay
    {
        IViewableDelegate<SessionData> GameFound { get; }
    }
}