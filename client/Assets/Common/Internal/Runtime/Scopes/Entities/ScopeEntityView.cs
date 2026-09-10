using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Internal
{
    [DisallowMultipleComponent]
    public class ScopeEntityView : MonoBehaviour, IScopeEntityView
    {
        [SerializeField] private List<MonoBehaviour> _autoDetected;

        private IContainer _container;

        public void CreateViews(IEntityBuilder builder)
        {
            foreach (var behaviour in _autoDetected)
            {
                if (behaviour is not IEntityComponent component)
                    throw new Exception();

                component.Register(builder);
            }
        }

        public void Bind(IContainer container)
        {
            _container = container;
        }

        [Button("Scan")]
        private void OnValidate()
        {
            _autoDetected ??= new();
            _autoDetected.Clear();
            var components = GetComponentsInChildren<IEntityComponent>(true);

            foreach (var component in components)
            {
                if (component is not MonoBehaviour behaviour)
                    throw new Exception();

                _autoDetected.Add(behaviour);
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        public void Dispose()
        {
            _container?.Dispose();
        }
    }
}