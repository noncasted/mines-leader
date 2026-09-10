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

        public BackendEnvironment Environment
        {
            get => _environment;
            set => _environment = value;
        }

        public string ProductionApiUrl
        {
            get => _productionApiUrl;
            set => _productionApiUrl = value;
        }

        public string LocalApiUrl
        {
            get => _localApiUrl;
            set => _localApiUrl = value;
        }

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
                    BackendEnvironment.Local => Url.Replace("http", "ws"),
                    BackendEnvironment.Production => Url.Replace("https", "wss"),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        }
    }
}