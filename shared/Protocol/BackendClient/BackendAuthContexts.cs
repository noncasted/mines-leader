using System;

namespace Shared
{
    public class BackendAuthContexts
    {
        public const string Endpoint = "/develop_signup";

        public class Request
        {
            public string Name { get; set; }
        }

        public class Response
        {
            public Guid Id { get; set; }
        }
    }
}