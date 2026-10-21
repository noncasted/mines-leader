using System;

namespace Meta
{
    public class SessionData
    {
        public SessionData(string serverUrl, Guid sessionId, string ticket)
        {
            ServerUrl = serverUrl;
            SessionId = sessionId;
            Ticket = ticket;
        }

        public string ServerUrl { get; }
        public Guid SessionId { get; }

        // S4: the entry of this user into the session (SharedSessionAuth.Request.Ticket).
        public string Ticket { get; }
    }
}