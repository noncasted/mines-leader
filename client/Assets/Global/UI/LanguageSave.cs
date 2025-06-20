using System;
using Global.Publisher;

namespace Global.UI
{
    [Serializable]
    public class LanguageSave
    {
        public bool IsOverriden { get; set; } = false;
        public Language Language { get; set; } = Language.Ru;
    }
}