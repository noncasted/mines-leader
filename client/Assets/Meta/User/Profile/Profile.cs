using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;
using UnityEngine;

namespace Meta
{
    public interface IProfile
    {
        Guid Id { get; }
        CharacterType Character { get; }

        IViewableProperty<string> Name { get; }
        IViewableProperty<int> Wins { get; }
        IViewableProperty<int> Loses { get; }
        IViewableProperty<int> Rating { get; }

        UniTask<IReadOnlyList<ProfileMatchEntry>> LoadHistory(int count);
        UniTask<ProfileMatchDetails> LoadDetails(Guid matchId);
    }

    public class Profile : IProfile, IScopeSetup
    {
        public const int DefaultHistoryCount = 30;

        public Profile(
            IMetaBackend backend,
            IBackendProjection<SharedBackendUser.ProfileProjection> profileProjection,
            IBackendProjection<SharedBackendUser.UserStatsProjection> statsProjection,
            IBackendProjection<SharedBackendUser.RatingProjection> ratingProjection,
            ILog<Profile> log)
        {
            _log = log;
            _backend = backend;
            _profileProjection = profileProjection;
            _statsProjection = statsProjection;
            _ratingProjection = ratingProjection;
        }

        private readonly ILog _log;
        private readonly IMetaBackend _backend;
        private readonly IBackendProjection<SharedBackendUser.ProfileProjection> _profileProjection;
        private readonly IBackendProjection<SharedBackendUser.UserStatsProjection> _statsProjection;
        private readonly IBackendProjection<SharedBackendUser.RatingProjection> _ratingProjection;

        private readonly ViewableProperty<string> _name = new(string.Empty);
        private readonly ViewableProperty<int> _wins = new(0);
        private readonly ViewableProperty<int> _loses = new(0);
        private readonly ViewableProperty<int> _rating = new(0);

        public Guid Id { get; private set; }
        public CharacterType Character => CharacterType.BIBA;

        public IViewableProperty<string> Name => _name;
        public IViewableProperty<int> Wins => _wins;
        public IViewableProperty<int> Loses => _loses;
        public IViewableProperty<int> Rating => _rating;

        public void OnSetup(IReadOnlyLifetime lifetime)
        {
            // Проекции едут параллельно сетапу меты: до подключения значений ещё нет.
            _profileProjection.ViewNotNull(lifetime, projection => {
                Id = projection.Id;
                _name.Set(projection.Name);
            });

            _ratingProjection.ViewNotNull(lifetime, projection => _rating.Set(projection.Rating));

            _statsProjection.ViewNotNull(lifetime, projection => {
                _wins.Set((int)projection.Get(UserStatType.MatchesWon));
                _loses.Set((int)projection.Get(UserStatType.MatchesLost));
            });
        }

        public async UniTask<IReadOnlyList<ProfileMatchEntry>> LoadHistory(int count)
        {
            var response = await _backend.GetMatchHistory(count);

            if (response?.Matches == null)
            {
                _log.Error("Match history request returned no data");
                return Array.Empty<ProfileMatchEntry>();
            }

            var userId = Id;
            var entries = new List<ProfileMatchEntry>(response.Matches.Count);

            foreach (var match in response.Matches)
            {
                entries.Add(new ProfileMatchEntry(
                    id: match.Id,
                    type: match.Type,
                    opponentName: match.OpponentName,
                    won: match.Winner == userId,
                    date: match.Date,
                    time: match.Time));
            }

            return entries;
        }

        public async UniTask<ProfileMatchDetails> LoadDetails(Guid matchId)
        {
            var response = await _backend.GetMatchDetails(matchId);

            if (response == null)
            {
                _log.Error("Match details request for {MatchId} returned no data", matchId);
                return null;
            }

            return new ProfileMatchDetails(
                id: response.MatchId,
                type: response.Type,
                opponentName: response.OpponentName,
                won: response.Won,
                time: response.Time,
                ratingChange: response.RatingChange,
                ownCards: response.OwnCards ?? new List<CardType>(),
                opponentCards: response.OpponentCards ?? new List<CardType>());
        }
    }
}