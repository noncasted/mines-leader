namespace Shared
{
    public enum ErrorCode : byte
    {
        Internal = 1,
        UnknownCommand = 2,
        InvalidRequest = 3,
        Forbidden = 4,
        NotFound = 5
    }
}
