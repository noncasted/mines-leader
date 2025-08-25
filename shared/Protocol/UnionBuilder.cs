using System;
using System.Collections.Generic;
using MemoryPack;
using MemoryPack.Formatters;

namespace Shared
{
    public interface IUnionBuilder<T> where T : class
    {
        IUnionBuilder<T> Add<TImplementation>() where TImplementation : T;
    }

    public class UnionBuilder<T> : IUnionBuilder<T> where T : class
    {
        private readonly List<Type> _items = new();
        
        public IUnionBuilder<T> Add<TImplementation>() where TImplementation : T
        {
            _items.Add(typeof(TImplementation));
            return this;
        }

        public void Build()
        {
            var types = new (ushort, Type)[_items.Count];
            
            for (ushort i = 0; i < _items.Count; i++)
                types[i] = (i, _items[i]);
            
            var formatter = new DynamicUnionFormatter<T>(types);
            MemoryPackFormatterProvider.Register(formatter);
        }
    }
}