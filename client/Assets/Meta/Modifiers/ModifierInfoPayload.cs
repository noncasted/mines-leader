using System;

namespace Meta
{
    [Serializable]
    public class ModifierInfoPayload
    {
        public string type;
        public string name;
        public string description;
    }

    [Serializable]
    public class ModifiersInfoPayload
    {
        public ModifierInfoPayload[] buffs;
    }
}
