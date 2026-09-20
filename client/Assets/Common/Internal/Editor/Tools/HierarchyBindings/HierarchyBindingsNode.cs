using System.Collections.Generic;
using UnityEngine;

namespace Internal
{
    // Зеркало одного объекта иерархии: компоненты становятся полями, дети — вложенными классами.
    internal sealed class HierarchyBindingsNode
    {
        public GameObject Target;
        public string HierarchyPath;

        // Имя вложенного класса; у корня это имя самого сгенерированного MonoBehaviour.
        public string TypeName;

        // У корня пусто: он не является полем ни в ком.
        public string PropertyName;
        public string FieldName;

        // На объекте висит HierarchyBindingsIgnoreChildren: дети в зеркало не попали.
        public bool IgnoreChildren;

        // Подряд идущие одинаковые братья схлопнуты в массив. Этот узел — первый из них и служит
        // шаблоном: класс генерируется по нему, а ссылки заполняются по каждому элементу.
        // У одиночного ребёнка список пуст.
        public readonly List<HierarchyBindingsNode> Elements = new();

        public bool IsArray => Elements.Count > 0;

        public readonly List<HierarchyBindingsField> Fields = new();
        public readonly List<HierarchyBindingsNode> Children = new();
    }

    // Поле-ссылка на компонент. Граничные объекты (свои биндинги, вложенный префаб) попадают
    // сюда же: снаружи они выглядят как обычная ссылка, внутрь генератор не заходит.
    internal sealed class HierarchyBindingsField
    {
        public UnityEngine.Object Target;

        // Схлопнутая группа граничных братьев: поле становится массивом, а Target — первым элементом.
        public readonly List<UnityEngine.Object> Targets = new();

        public string PropertyName;
        public string FieldName;
        public string CodeTypeName;
        public string Comment;

        public bool IsArray => Targets.Count > 0;

        public string DeclaredTypeName => IsArray ? CodeTypeName + "[]" : CodeTypeName;
    }
}
