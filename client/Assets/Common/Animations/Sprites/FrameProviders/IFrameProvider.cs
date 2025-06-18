using UnityEngine;

namespace Common.Animations
{
    public interface IFrameProvider
    {
        int FrameCount { get; }
        
        Sprite GetFrame(int index);
    }
}