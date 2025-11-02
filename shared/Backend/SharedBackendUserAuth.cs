using System;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Shared
{
    public class SharedBackendUserSignUp
    {
        public const string Endpoint = "/develop_signup";

        public class Request
        {
        }

        public class Response
        {
            public Guid Id { get; set; }
        }
    }

    public class SharedBackendUserLogin
    {
        public const string Endpoint = "/login";

        public class Request
        {
            public Guid Id { get; set; }
        }

        public class Response
        {
        }
    }
}