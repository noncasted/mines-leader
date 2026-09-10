using System;

namespace Internal
{
    // Метод инжекта. Генератор ищет его только по атрибуту, имя метода любое.
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class InjectAttribute : Attribute
    {
    }
}