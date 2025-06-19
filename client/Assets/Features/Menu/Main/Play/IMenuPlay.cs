using Internal;
using Meta;

namespace Menu
{
    public interface IMenuPlay
    {
        IViewableDelegate<SessionData> GameFound { get; }
    }
}