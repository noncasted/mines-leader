using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;
using UnityEngine;

namespace Meta
{
    public interface IAchievementRewards
    {
        /// <summary>
        /// Cards the player picks the reward from. The server fixes the set per achievement,
        /// so a repeated request returns the same list.
        /// </summary>
        UniTask<IReadOnlyList<CardType>> GetOptions(IAchievementTier tier);

        /// <summary>
        /// Claims the picked card. The tier status arrives as a projection.
        /// </summary>
        UniTask<bool> Claim(IAchievementTier tier, CardType card);
    }

    public class AchievementRewards : IAchievementRewards
    {
        public AchievementRewards(IMetaBackend backend, ILog<AchievementRewards> log)
        {
            _backend = backend;
            _log = log;
        }

        private readonly IMetaBackend _backend;
        private readonly ILog _log;

        public async UniTask<IReadOnlyList<CardType>> GetOptions(IAchievementTier tier)
        {
            // null - the request did not arrive or failed on the server: the caller tells this from an empty pool and closes the window.
            try
            {
                var response = await _backend.GetAchievementRewardOptions(tier.Type, tier.Tier);
                return response.Options;
            }
            catch (Exception e) when (e is NetworkRequestException || e is TimeoutException || e is OperationCanceledException)
            {
                _log.Exception(e);
                return null;
            }
        }

        public UniTask<bool> Claim(IAchievementTier tier, CardType card)
        {
            return _backend.ClaimAchievementReward(tier.Type, tier.Tier, card);
        }
    }
}