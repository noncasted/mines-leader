using System;

namespace Tools
{
    [AttributeUsage(AttributeTargets.Field)]
    public class InjectGeneratedAttribute : Attribute
    {
        public string Key { get; }

        public InjectGeneratedAttribute()
        {
            Key = string.Empty;
        }

        public InjectGeneratedAttribute(string key)
        {
            Key = key;
        }
    }
}