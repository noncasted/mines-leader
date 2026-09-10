using System;

namespace Internal
{
    // Цепочку в installer'ах пишут расширениями BuilderExtensions (As, AsSelf, WithParameter, ...):
    // по ним генератор строит граф. Методы здесь названы иначе, чтобы не перехватывать эти вызовы.
    public interface IServiceRegistration
    {
        // Билдер, в который записана регистрация: расширения цепочки регистрируют через него соседей.
        IBuilder Builder { get; }
        Type ImplementationType { get; }
        IServiceRegistration AddServiceType(Type serviceType);
        IServiceRegistration SetParameter(Type type, object value);
    }
}