using UnityEngine;
#if UNITY_EDITOR
using System;
using Unity.Scripting.LifecycleManagement;
#endif

namespace Internal
{
    // Общая база всех сгенерированных биндингов. Нужна не ради поведения, а ради адресации:
    // по ней находится сам сгенерированный класс, когда пользовательский наследует его,
    // и на ней живёт кнопка перегенерации.
#if UNITY_EDITOR
    [NoAutoStaticsCleanup]
#endif
    public abstract class ObjectBindings : MonoBehaviour, IObjectBindings
    {
        // Слепок иерархии на момент генерации. Живёт в базе, чтобы сгенерированный класс
        // состоял только из ссылок.
        [SerializeField, HideInInspector] private string _structureHash;

        // Регистрация в DI нужна не всем биндингам, поэтому интерфейсы генерируются по этим
        // галочкам. Значение хранится на компоненте, чтобы Regenerate знал, что писать.
        [SerializeField] private bool _isSceneService;
        [SerializeField] private bool _isEntityComponent;

        public string StructureHash => _structureHash;

        public bool IsSceneService => _isSceneService;

        public bool IsEntityComponent => _isEntityComponent;

#if UNITY_EDITOR
        // Рантайм-сборка не может ссылаться на редакторную, поэтому связь односторонняя:
        // генератор подставляет сюда себя на загрузке домена.
        public static Action<ObjectBindings> RegenerateHandler;

        [Button("Regenerate")]
        private void Regenerate()
        {
            if (RegenerateHandler == null)
            {
                Debug.LogError("[HierarchyBindingsGenerator] Generator is not loaded.", this);
                return;
            }

            RegenerateHandler.Invoke(this);
        }
#endif
    }
}