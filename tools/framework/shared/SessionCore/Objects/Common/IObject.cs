using System;
using System.Collections.Generic;
using System.Linq;
using Internal;

namespace Game.Session
{
    public interface IObject
    {
        int Id { get; }
        IReadOnlyLifetime Lifetime { get; }

        IReadOnlyDictionary<int, IObjectProperty> Properties { get; }
    }
}
