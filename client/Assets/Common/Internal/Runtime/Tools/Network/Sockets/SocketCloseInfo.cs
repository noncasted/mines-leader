namespace Internal
{
    // Why a socket was lost: the close code (from the Close frame of the server, or 1006 for a broken connection
    // without one, as a browser reports it) and a reason for the log.
    public readonly struct SocketCloseInfo
    {
        public SocketCloseInfo(int code, string reason)
        {
            Code = code;
            Reason = reason;
        }

        // RFC 6455: the connection was closed without a Close frame.
        public const int AbnormalClosure = 1006;

        // RFC 6455: a Close frame without a status code.
        public const int NoStatusReceived = 1005;

        public readonly int Code;
        public readonly string Reason;

        public override string ToString()
        {
            return $"code={Code} {Reason}";
        }
    }
}
