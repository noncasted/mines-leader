using System.Collections.Generic;

namespace Global.Backend
{
    public interface IGetRequest
    {
        string Uri { get; }
        IReadOnlyList<IRequestHeader> Headers { get; }
    }
}