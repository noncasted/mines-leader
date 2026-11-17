using System;

namespace Game.Session
{
    // The log of a session: the backend writes it to the providers of the host (ILogger), a host client to the Unity
    // console. The messages are complete strings: the core does not know the structured logging of the backend.
    public interface ISessionLog
    {
        void Debug(string message);
        void Info(string message);
        void Warning(string message);
        void Error(string message, Exception exception = null);
    }

    // A log that writes nothing: the tests of the commands.
    public sealed class NullSessionLog : ISessionLog
    {
        public void Debug(string message)
        {
        }

        public void Info(string message)
        {
        }

        public void Warning(string message)
        {
        }

        public void Error(string message, Exception exception = null)
        {
        }
    }
}
