using System;

namespace Internal
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class ButtonAttribute : Attribute
    {
        public string Text { get; }

        public ButtonAttribute(string text = null)
        {
            Text = text;
        }
    }
}
