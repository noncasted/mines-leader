using System;
using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    public interface IAssetEnvironment
    {
        T GetAsset<T>() where T : ScriptableObject;
        IReadOnlyList<T> GetAssets<T>() where T : ScriptableObject;
        T GetOptions<T>() where T : class, IOptionsEntry;
    }

    public static class AssetsEnvironmentExtensions
    {
        public static IScopeBuilder RegisterEnvDictionary<TKey, TValue, TSource>(this IScopeBuilder builder)
            where TSource : EnvAsset, IEnvDictionaryKeyProvider<TKey>, TValue
        {
            var assets = builder.Assets.GetAssets<TSource>();

            var dictionary = new EnvDictionary<TKey, TValue>();

            foreach (var asset in assets)
            {
                if (asset is not IEnvDictionaryKeyProvider<TKey> keyProvider)
                    throw new Exception();

                dictionary.Add(keyProvider.EnvKey, asset);
            }

            builder.RegisterInstance(dictionary)
                .As<IEnvDictionary<TKey, TValue>>();

            return builder;
        }
    }
}