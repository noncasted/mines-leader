using Internal;

namespace Menu
{
    public interface IMenuChatUI
    {
        IViewableDelegate<string> MessageSend { get; }
    }
}