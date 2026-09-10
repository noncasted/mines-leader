using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Internal
{
    [InlineEditor]
    [CreateAssetMenu(fileName = "AnimatorLayer", menuName = "Common/Animator/ForwardAnimation")]
    public class ForwardAnimationAsset : ScriptableObject
    {
        [SerializeField] private Sprite[] _sprites;
        [SerializeField] private float _time = 0.8f;
        [SerializeField] private Color _color = Color.white;

        public IReadOnlyList<Sprite> Sprites => _sprites;
        public float Time => _time;
        public Color Color => _color;
    }

    [Serializable]
    public class ForwardAnimationData
    {
        [SerializeField] private Sprite[] _sprites;
        [SerializeField] private float _time = 0.8f;
        [SerializeField] private Color _color = Color.white;

        public IReadOnlyList<Sprite> Sprites => _sprites;
        public float Time => _time;
        public Color Color => _color;
    }
}