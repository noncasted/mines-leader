using System;
using System.Collections.Generic;
using Internal;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Common.Animations
{
    [InlineEditor]
    public class ForwardAnimationAsset : EnvAsset
    {
        [SerializeField] private Sprite[] _sprites;
        [SerializeField] private float _time = 0.8f;

        public IReadOnlyList<Sprite> Sprites => _sprites;
        public float Time => _time;
    }

    [Serializable]
    public class ForwardAnimationData
    {
        [SerializeField] private Sprite[] _sprites;
        [SerializeField] private float _time = 0.8f;

        public IReadOnlyList<Sprite> Sprites => _sprites;
        public float Time => _time;
    }
}