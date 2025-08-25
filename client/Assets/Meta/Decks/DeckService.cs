using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Meta
{
    public interface IDeckService
    {
        IReadOnlyDictionary<int, IDeckConfiguration> Configurations { get; }
        IViewableProperty<int> SelectedIndex { get; }
        IViewableDelegate Updated { get; }

        UniTask SendUpdate();
        void SetIndex(int selectedIndex);
    }

    public class DeckService : IDeckService, IScopeSetup
    {
        public DeckService(
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
            _projection.Listen(lifetime, data =>
            {
                foreach (var (index, entry) in data.Entries)
                {
                    var configuration = GetOrCreateConfiguration(index);
                    var cards = new List<ICardDefinition>();

                    foreach (var cardType in entry.Cards)
                    {
                        var definition = _cardsRegistry.Cards[cardType];
                        cards.Add(definition);
                    }

                    configuration.Update(cards);
                }

                _selectedIndex.Set(data.SelectedIndex);
                _updated.Invoke();
            });
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
                        })
                }
            };

            return _backend.ExecuteCommand(request);
        }

        public void SetIndex(int selectedIndex)
        {
            _selectedIndex.Set(selectedIndex);
        }

        private IDeckConfiguration GetOrCreateConfiguration(int index)
        {
            if (_configurations.TryGetValue(index, out var configuration))
                return configuration;

            configuration = new DeckConfiguration(index);

            _configurations[index] = configuration;

            return configuration;
        }
    }
}