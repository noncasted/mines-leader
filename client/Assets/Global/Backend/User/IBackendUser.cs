using System;
using Internal;

namespace Global.Backend
{
    public interface IBackendUser
    {
        Guid Id { get; }
        IViewableProperty<string> Name { get; }
    }
}