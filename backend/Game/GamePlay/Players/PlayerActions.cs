using Common.Reactive;

namespace Game.GamePlay;

public interface IPlayerActions
{
    IViewableDelegate CellOpened { get; }
    IViewableDelegate CardUsed { get; }

    void OnCellOpened();
    void OnCardUsed();
}

public class PlayerActions : IPlayerActions
{
    private readonly ViewableDelegate _cellOpened = new();
    private readonly ViewableDelegate _cardUsed = new();

    public IViewableDelegate CellOpened => _cellOpened;
    public IViewableDelegate CardUsed => _cardUsed;

    public void OnCellOpened()
    {
        _cellOpened.Invoke();
    }

    public void OnCardUsed()
    {
        _cardUsed.Invoke();
    }
}