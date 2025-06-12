using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Internal
{
    public class AssetEnvironment : IAssetEnvironment
    {
        public AssetEnvironment(
            IAssetsStorage assetsStorage,
            OptionsRegistry optionsRegistry)
        {
            _assetsStorage = assetsStorage;
            _optionsRegistry = optionsRegistry;
        }

        private readonly IAssetsStorage _assetsStorage;
        private readonly OptionsRegistry _optionsRegistry;

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

        public T GetOptions<T>() where T : class, IOptionsEntry
        {
            if (_optionsRegistry.TryGetEntry<T>(out var options) == true)
                return options;

            throw new NullReferenceException();
        }
    }
}