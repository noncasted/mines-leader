using System;

namespace Common
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class SharedGrainStateAttribute : Attribute
    {
        public string Table { get; set; }
        public string State { get; set; }
        public string Lookup { get; set; }
        public GrainKeyType Key { get; set; }
    }
}