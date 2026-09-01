using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;
using UnityEngine;

namespace Meta
{
    /// <summary>
    /// Одна строка истории матчей. Приезжает пачкой по запросу, реактивных обновлений нет:
    /// история меняется только между входами на экран профиля.
    /// </summary>
    public class ProfileMatchEntry
    {
        public Guid Id { get; set; }
        public GameMatchType Type { get; set; }
        public string OpponentName { get; set; }
        public bool Won { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan Time { get; set; }
    }

    /// <summary>Развёрнутый матч: колоды обеих сторон, время и изменение рейтинга.</summary>
    public class ProfileMatchDetails
    {
        public Guid Id { get; set; }
        public GameMatchType Type { get; set; }
        public string OpponentName { get; set; }
        public bool Won { get; set; }
        public TimeSpan Time { get; set; }
        public int RatingChange { get; set; }
        public IReadOnlyList<CardType> OwnCards { get; set; }
        public IReadOnlyList<CardType> OpponentCards { get; set; }
    }

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
            IBackendProjection<SharedBackendUser.RatingProjection> ratingProjection)
        {
            _backend = backend;
            _profileProjection = profileProjection;
            _statsProjection = statsProjection;
            _ratingProjection = ratingProjection;
        }

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
            _profileProjection.Listen(lifetime, projection => {
                Id = projection.Id;
                _name.Set(projection.Name);
            });
            _ratingProjection.Listen(lifetime, projection => _rating.Set(projection.Rating));

            _statsProjection.Listen(lifetime, projection => {
                _wins.Set((int)projection.Get(UserStatType.MatchesWon));
                _loses.Set((int)projection.Get(UserStatType.MatchesLost));
            });
        }

        public async UniTask<IReadOnlyList<ProfileMatchEntry>> LoadHistory(int count)
        {
            var response = await _backend.GetMatchHistory(count);

            if (response?.Matches == null)
            {
                Debug.LogError("[Profile] Match history request returned no data");
                return Array.Empty<ProfileMatchEntry>();
            }

            var userId = Id;
            var entries = new List<ProfileMatchEntry>(response.Matches.Count);

            foreach (var match in response.Matches)
            {
                entries.Add(new ProfileMatchEntry
                {
                    Id = match.Id,
                    Type = match.Type,
                    OpponentName = match.OpponentName,
                    Won = match.Winner == userId,
                    Date = match.Date,
                    Time = match.Time
                });
            }

            return entries;
        }

        public async UniTask<ProfileMatchDetails> LoadDetails(Guid matchId)
        {
            var response = await _backend.GetMatchDetails(matchId);

            if (response == null)
            {
                Debug.LogError($"[Profile] Match details request for {matchId} returned no data");
                return null;
            }

            return new ProfileMatchDetails
            {
                Id = response.MatchId,
                Type = response.Type,
                OpponentName = response.OpponentName,
                Won = response.Won,
                Time = response.Time,
                RatingChange = response.RatingChange,
                OwnCards = response.OwnCards ?? new List<CardType>(),
                OpponentCards = response.OpponentCards ?? new List<CardType>()
            };
        }
    }
}
