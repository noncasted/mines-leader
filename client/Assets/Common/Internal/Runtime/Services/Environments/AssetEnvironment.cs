using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Internal
{
    public interface IAssetEnvironment
    {
        T GetAsset<T>() where T : ScriptableObject;
        IReadOnlyList<T> GetAssets<T>() where T : ScriptableObject;
    }

    public class AssetEnvironment : IAssetEnvironment
    {
        public AssetEnvironment(IAssetsStorage assetsStorage)
        {
            _assetsStorage = assetsStorage;
        }

        private readonly IAssetsStorage _assetsStorage;

        public T GetAsset<T>() where T : ScriptableObject
        {
            var type = typeof(T);

            var assetCollection = _assetsStorage.Assets[type.FullName];

            if (assetCollection.Count != 1)
                throw new Exception();

            return assetCollection.First() as T;
        }

        public IReadOnlyList<T> GetAssets<T>() where T : ScriptableObject
        {
            var collection = _assetsStorage.Assets[typeof(T).FullName];
            var result = new List<T>(collection.Count);

            foreach (var asset in collection)
                result.Add(asset as T);

            return result;
        }
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