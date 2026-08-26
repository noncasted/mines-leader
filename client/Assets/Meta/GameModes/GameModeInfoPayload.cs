using System;

namespace Meta
{
    [Serializable]
    public class GameModeInfoPayload
    {
        public string type;
        public string name;
        public string description;
    }

    [Serializable]
    public class GameModesInfoPayload
    {
        public GameModeInfoPayload[] modes;
    }
}
