using GamePlay.Loop;
using Internal;

namespace GamePlay.Cards
{
    public interface ICardPointerHandler
    {
        IViewableProperty<bool> IsHovered { get; }
        IViewableProperty<bool> IsPressed { get; }
    }

    // Сырые события указателя приходят из CardPointerEvents, здесь к ним добавляется
    // единственное правило: на паузе карта не подсвечивается и не нажимается.
    public class CardPointerHandler : ICardPointerHandler, IScopeSetup
    {
        public CardPointerHandler(GameCardBindings bindings, IGameContext gameContext)
        {
            _events = bindings.View.PointerHandler.CardPointerEvents;
            _gameContext = gameContext;
        }

        private readonly CardPointerEvents _events;
        private readonly IGameContext _gameContext;

        private readonly ViewableProperty<bool> _isHovered = new();
        private readonly ViewableProperty<bool> _isPressed = new();

        public IViewableProperty<bool> IsHovered => _isHovered;
        public IViewableProperty<bool> IsPressed => _isPressed;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _events.IsOver.Advise(lifetime, value => _isHovered.Set(value && _gameContext.IsPaused == false));
            _events.IsDown.Advise(lifetime, value => _isPressed.Set(value && _gameContext.IsPaused == false));
        }
    }

    public static class CardPointerHandlerExtensions
    {
        public static IReadOnlyLifetime GetUpAwaiterLifetime(
            this ICardPointerHandler pointerHandler,
            IReadOnlyLifetime lifetime)
        {
            var childLifetime = lifetime.Child();

            pointerHandler.IsPressed.Advise(childLifetime, value => {
                if (value == true)
                    return;

                childLifetime.Terminate();
            });

            return childLifetime;
        }
    }
}
