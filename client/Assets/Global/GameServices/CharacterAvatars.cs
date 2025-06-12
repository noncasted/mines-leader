using System;
using System.Collections.Generic;
using Internal;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Global.GameServices
{
    [InlineEditor]
    public class CharacterAvatars : EnvAsset
    {
        [SerializeField] private CharacterAvatarsDictionary _value;
        
        public IReadOnlyDictionary<CharacterType, Sprite> Value => _value;
    }

    [Serializable]
    public class CharacterAvatarsDictionary : SerializableDictionary<CharacterType, Sprite>
    {
    }
}