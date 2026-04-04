using System;
using UnityEngine;

namespace Internal
{
    [Serializable]
    public class VersionOptions
    {
        [SerializeField] private string _value = "1.0.0";

        public string Value
        {
            get => _value;
            set => _value = value;
        }
    }
}