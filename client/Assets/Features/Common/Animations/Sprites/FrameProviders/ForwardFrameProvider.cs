using System.Collections.Generic;
using UnityEngine;

namespace Common.Animations
{
    public class ForwardFrameProvider : IFrameProvider
    {
        public ForwardFrameProvider(IReadOnlyList<Sprite> frames)
        {
            _frames = frames;
        }

        private readonly IReadOnlyList<Sprite> _frames;

        public int FrameCount => _frames.Count;

        public Sprite GetFrame(int index)
        {
            return _frames[index];
        }
    }
}