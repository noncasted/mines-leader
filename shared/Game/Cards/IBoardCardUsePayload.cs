namespace Shared
{
    public interface IBoardCardUsePayload : ICardUsePayload
    {
        Position Position { get; set; }
    }
}
