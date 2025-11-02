using System;
using UnityEngine;

namespace Internal {
    [Serializable]
    public class AssetsOptions {
        [SerializeField] private bool _useAddressables;

        public bool UseAddressables {
            get => _useAddressables;
            set => _useAddressables = value;
        }
    }
}
