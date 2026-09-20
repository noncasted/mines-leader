using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    // Единственный MonoBehaviour карты кроме биндингов: сообщения мыши Unity шлёт только
    // компоненту рядом с коллайдером, поэтому логика карты читает их отсюда через биндинги.
    // Здесь нет ни одного игрового правила — только сырое состояние указателя.
    [DisallowMultipleComponent]
    public class CardPointerEvents : MonoBehaviour
    {
        private readonly ViewableProperty<bool> _isOver = new();
        private readonly ViewableProperty<bool> _isDown = new();

        public IViewableProperty<bool> IsOver => _isOver;
        public IViewableProperty<bool> IsDown => _isDown;

        // Удалённая карта выключает этот объект: указатель на ней не живёт, и состояние
        // должно погаснуть, а не залипнуть последним значением.
        private void OnDisable()
        {
            _isOver.Set(false);
            _isDown.Set(false);
        }

        private void OnMouseEnter()
        {
            _isOver.Set(true);
        }

        private void OnMouseOver()
        {
            _isOver.Set(true);
        }

        private void OnMouseExit()
        {
            _isOver.Set(false);
        }

        private void OnMouseDown()
        {
            _isDown.Set(true);
        }

        private void OnMouseUp()
        {
            _isDown.Set(false);
        }
    }
}
