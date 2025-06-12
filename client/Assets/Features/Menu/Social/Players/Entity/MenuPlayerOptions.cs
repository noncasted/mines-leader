using Internal;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Menu
{
    [InlineEditor]
    public class MenuPlayerOptions : EnvAsset
    {
        [SerializeField] private MenuPlayer _prefab;
        [SerializeField] private float _moveSpeed = 1f;
        [SerializeField] private float _lerpSpeed = 1f;
        
        public float MoveSpeed => _moveSpeed;
        public float LerpSpeed => _lerpSpeed;
        public MenuPlayer Prefab => _prefab;
    }
}