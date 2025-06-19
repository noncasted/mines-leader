using System.Collections.Generic;

namespace Global.Backend
{
    public interface IGetRequest
    {
        string Uri { get; }
        IReadOnlyList<IRequestHeader> Headers { get; }
    }
    
    public class GetRequest : IGetRequest
    {
        public GetRequest(string url, IReadOnlyList<IRequestHeader> headers)
        {
            Uri = url;
            Headers = headers;
        }

        public string Uri { get; }
        public IReadOnlyList<IRequestHeader> Headers { get; }
    }
}