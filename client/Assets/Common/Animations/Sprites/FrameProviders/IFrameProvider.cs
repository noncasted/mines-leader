using UnityEngine;

namespace Animations
{
    public interface IFrameProvider
    {
        int FrameCount { get; }

        Sprite GetFrame(int index);
    }
}