using System;
using Internal;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Global.Backend
{
    [InlineEditor]
    [CreateAssetMenu(fileName = "Options_Backend", menuName = "Internal/Options/Backend")]
    public class BackendOptions : EnvAsset
    {
        [SerializeField] private BackendEnvironment _environment;

        [ShowIf("_environment", BackendEnvironment.Production)] [SerializeField]
        private string _productionApiUrl;

        [ShowIf("_environment", BackendEnvironment.Local)] [SerializeField]
        private string _localApiUrl;

        public string Url
        {
            get
            {
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

    public enum BackendEnvironment
    {
        Local,
        Production
    }
}