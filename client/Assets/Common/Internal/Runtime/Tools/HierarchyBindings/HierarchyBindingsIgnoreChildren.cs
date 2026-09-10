using UnityEngine;

namespace Internal
{
    // Обрывает зеркало на этом объекте: сам он в биндингах остаётся вместе со своими
    // компонентами, а дети — нет. Нужен там, где под объектом лежит разметка, которая в код
    // не просится: контент скроллов, спавн-рутов и прочих контейнеров.
    [DisallowMultipleComponent]
    public sealed class HierarchyBindingsIgnoreChildren : MonoBehaviour
    {
    }
}