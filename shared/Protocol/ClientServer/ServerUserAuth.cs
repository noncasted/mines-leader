using System;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shared
{
    public class ServerUserAuth
    {
        public Guid UserId { get; set; }
        public Guid SessionId { get; set; }
    }
}