using System;

namespace Game.Session
{
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
