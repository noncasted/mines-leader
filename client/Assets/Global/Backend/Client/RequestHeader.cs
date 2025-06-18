namespace Global.Backend
{
    public interface IRequestHeader
    {
        string Type { get; }
        string Value { get; }
    }
    
    public class RequestHeader : IRequestHeader
    {
        public RequestHeader(string type, string value)
        {
            Type = type;
            Value = value;
        }

        public string Type { get; }
        public string Value { get; }

        public static IRequestHeader Json()
        {
            return new RequestHeader("Content-Type", "application/json");
        }
    }
}