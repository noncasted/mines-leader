using Common.Reactive;
using Shared;

namespace Game.GamePlay;

public interface IPlayerActions
{
    IViewableDelegate CellOpened { get; }
    IViewableDelegate CardUsed { get; }
    CardType? LastUsedCardType { get; }
    ICardUsePayload? LastUsedPayload { get; }

    void OnCellOpened();
    void OnCardUsed(CardType cardType, ICardUsePayload payload);
}

public class PlayerActions : IPlayerActions
{
    private readonly ViewableDelegate _cellOpened = new();
    private readonly ViewableDelegate _cardUsed = new();

    public IViewableDelegate CellOpened => _cellOpened;
    public IViewableDelegate CardUsed => _cardUsed;
    public CardType? LastUsedCardType { get; private set; }
    public ICardUsePayload? LastUsedPayload { get; private set; }

    public void OnCellOpened()
    {
        _cellOpened.Invoke();
    }

    public void OnCardUsed(CardType cardType, ICardUsePayload payload)
    {
        LastUsedCardType = cardType;
        LastUsedPayload = payload;
        _cardUsed.Invoke();
    }
}