using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Global.Backend;
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
            IUser user,
            IBackendProjection<BackendUserContexts.DeckProjection> projection,
            IBackendClient client,
            IReadOnlyLifetime lifetime)
        {
            _cardsRegistry = cardsRegistry;
            _user = user;
            _projection = projection;
            _client = client;
            _lifetime = lifetime;
        }

        private readonly Dictionary<int, IDeckConfiguration> _configurations = new();
        private readonly ViewableProperty<int> _selectedIndex = new(0);
        private readonly ICardsRegistry _cardsRegistry;
        private readonly IUser _user;
        private readonly IBackendProjection<BackendUserContexts.DeckProjection> _projection;
        private readonly IBackendClient _client;
        private readonly IReadOnlyLifetime _lifetime;
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
            var request = new BackendUserContexts.UpdateDeckRequest()
            {
                UserId = _user.Id,
                Projection = new BackendUserContexts.DeckProjection()
                {
                    SelectedIndex = _selectedIndex.Value,
                    Entries = _configurations.ToDictionary(
                        x => x.Key,
                        x => new BackendUserContexts.DeckProjection.Entry()
                        {
                            DeckIndex = x.Key,
                            Cards = x.Value.Cards.Select(x => x.Type).ToList()
                        })
                }
            };

            var endpoint = _client.Options.Url + BackendUserContexts.UpdateDeckEndpoint;
            return _client.PostJson(_lifetime, endpoint, request);
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