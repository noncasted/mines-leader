using System;

namespace Meta
{
    public class SessionData
    {
        public SessionData(string serverUrl, Guid sessionId)
        {
            ServerUrl = serverUrl;
            SessionId = sessionId;
        }

        public string ServerUrl { get; }
        public Guid SessionId { get; }
    }
}