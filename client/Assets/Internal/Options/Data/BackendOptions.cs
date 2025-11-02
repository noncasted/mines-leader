using System;
using UnityEngine;

namespace Internal
{
    public enum BackendEnvironment
    {
        Local,
        Production
    }

    [Serializable]
    public class BackendOptions
    {
        [SerializeField] private BackendEnvironment _environment;
        [SerializeField] private string _productionApiUrl = "https://gateway.minesleader.xyz";
        [SerializeField] private string _localApiUrl = "http://localhost:5003";

        public BackendEnvironment Environment => _environment;

        public string Url
        {
            get
            {
                if (Application.isEditor == false)
                    return _productionApiUrl;

                return _environment switch
                {
                    BackendEnvironment.Local => _localApiUrl,
                    BackendEnvironment.Production => _productionApiUrl,
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        }

        public string SocketUrl
        {
            get
            {
                return _environment switch
                {
                    BackendEnvironment.Local => _localApiUrl.Replace("http", "ws"),
                    BackendEnvironment.Production => _productionApiUrl.Replace("https", "wss"),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        }
    }
}