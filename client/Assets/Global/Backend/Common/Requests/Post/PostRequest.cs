using System.Collections.Generic;

namespace Global.Backend
{
    public class PostRequest : IPostRequest
    {
        public PostRequest(string url, string body, IReadOnlyList<IRequestHeader> headers)
        {
            Uri = url;
            Body = body;
            Headers = headers;
        }

        public string Uri { get; }
        public string Body { get; }
        public IReadOnlyList<IRequestHeader> Headers { get; }
    }
}