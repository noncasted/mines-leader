using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Meta
{
    public interface IDecks
    {
        IReadOnlyDictionary<int, IDeckConfiguration> Configurations { get; }
        IViewableProperty<int> SelectedIndex { get; }
        IViewableDelegate Updated { get; }

        UniTask SendUpdate();
        void SetIndex(int selectedIndex);
    }

    public class Decks : IDecks, IScopeSetup
    {
        public Decks(
            ICardsRegistry cardsRegistry,
            IBackendProjection<SharedBackendUser.DeckProjection> projection,
            IMetaBackend backend)
        {
            _cardsRegistry = cardsRegistry;
            _projection = projection;
            _backend = backend;
        }

        private readonly Dictionary<int, IDeckConfiguration> _configurations = new();
        private readonly ViewableProperty<int> _selectedIndex = new(0);
        private readonly ICardsRegistry _cardsRegistry;
        private readonly IBackendProjection<SharedBackendUser.DeckProjection> _projection;
        private readonly IMetaBackend _backend;
        private readonly ViewableDelegate _updated = new();

        public IReadOnlyDictionary<int, IDeckConfiguration> Configurations => _configurations;
        public IViewableProperty<int> SelectedIndex => _selectedIndex;

        public IViewableDelegate Updated => _updated;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            _projection.View(lifetime, data => {
                foreach (var (index, entry) in data.Entries)
                {
                    var cards = GetDefinitions(entry.Cards);

                    if (_configurations.TryGetValue(index, out var configuration) == true)
                    {
                        configuration.Update(cards);
                    }
                    else
                    {
                        configuration = new DeckConfiguration(index, cards);

                        _configurations[index] = configuration;
                    }
                }

                _selectedIndex.Set(data.SelectedIndex);
                _updated.Invoke();
            });

            return;

            IReadOnlyList<ICardDefinition> GetDefinitions(IReadOnlyList<CardType> cardTypes)
            {
                var cards = new List<ICardDefinition>();

                foreach (var cardType in cardTypes)
                {
                    var definition = _cardsRegistry.Entries[cardType];
                    cards.Add(definition);
                }

                return cards;
            }
        }

        public UniTask SendUpdate()
        {
            var request = new SharedBackendUser.UpdateDeckRequest()
            {
                Projection = new SharedBackendUser.DeckProjection()
                {
                    SelectedIndex = _selectedIndex.Value,
                    Entries = _configurations.ToDictionary(
                            x => x.Key,
                            x => new SharedBackendUser.DeckProjection.Entry()
                            {
                                DeckIndex = x.Key,
                                Cards = x.Value.Cards.Select(x => x.Type).ToList()
                            }
                        )
                }
            };

            return _backend.ExecuteCommand(request);
        }

        public void SetIndex(int selectedIndex)
        {
            _selectedIndex.Set(selectedIndex);
        }
    }
}