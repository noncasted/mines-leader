using UnityEngine;

namespace Internal
{
    public interface IFrameProvider
    {
        int FrameCount { get; }

        Sprite GetFrame(int index);
    }
}