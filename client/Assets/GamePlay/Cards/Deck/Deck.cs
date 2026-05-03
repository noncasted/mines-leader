namespace GamePlay.Cards
{
    public interface IDeck
    {
        IDeckView View { get; }

        void SetCount(int count);
    }

    public class Deck : IDeck
    {
        public Deck(IDeckView view)
        {
            _view = view;
        }

        private readonly IDeckView _view;

        public IDeckView View => _view;

        public void SetCount(int count)
        {
            _view.UpdateAmount(count);
        }
    }
}
