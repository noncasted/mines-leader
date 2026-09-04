using System.Collections.Generic;
using UnityEngine;

namespace Internal {
    // Зеркало одного объекта иерархии: компоненты становятся полями, дети — вложенными классами.
    internal sealed class HierarchyBindingsNode {
        public GameObject Target;
        public string HierarchyPath;

        // Имя вложенного класса; у корня это имя самого сгенерированного MonoBehaviour.
        public string TypeName;

        // У корня пусто: он не является полем ни в ком.
        public string PropertyName;
        public string FieldName;

        public readonly List<HierarchyBindingsField> Fields = new();
        public readonly List<HierarchyBindingsNode> Children = new();
    }

    // Поле-ссылка на компонент. Граничные объекты (свои биндинги, вложенный префаб) попадают
    // сюда же: снаружи они выглядят как обычная ссылка, внутрь генератор не заходит.
    internal sealed class HierarchyBindingsField {
        public UnityEngine.Object Target;
        public string PropertyName;
        public string FieldName;
        public string CodeTypeName;
        public string Comment;
    }
}
