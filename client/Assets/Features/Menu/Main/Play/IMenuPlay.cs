using Global.Backend;
using Internal;

namespace Menu
{
    public interface IMenuPlay
    {
        IViewableDelegate<SessionData> GameFound { get; }
    }
}