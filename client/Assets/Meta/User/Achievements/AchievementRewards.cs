using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Shared;
using UnityEngine;

namespace Meta
{
    public interface IAchievementRewards
    {
        /// <summary>
        /// Карты, из которых игрок выбирает награду. Сервер фиксирует набор за ачивкой,
        /// поэтому повторный запрос вернёт тот же список.
        /// </summary>
        UniTask<IReadOnlyList<CardType>> GetOptions(IAchievementTier tier);

        /// <summary>
        /// Забирает выбранную карту. Статус тира приедет проекцией.
        /// </summary>
        UniTask<bool> Claim(IAchievementTier tier, CardType card);
    }

    public class AchievementRewards : IAchievementRewards
    {
        public AchievementRewards(IMetaBackend backend)
        {
            _backend = backend;
        }

        private readonly IMetaBackend _backend;

        public async UniTask<IReadOnlyList<CardType>> GetOptions(IAchievementTier tier)
        {
            // null — запрос не дошёл или упал на сервере: вызывающий отличит это от пустого пула и закроет окно.
            try
            {
                var response = await _backend.GetAchievementRewardOptions(tier.Type, tier.Tier);
                return response.Options;
            }
            catch (Exception e) when (e is NetworkRequestException || e is TimeoutException || e is OperationCanceledException)
            {
                Debug.LogException(e);
                return null;
            }
        }

        public UniTask<bool> Claim(IAchievementTier tier, CardType card)
        {
            return _backend.ClaimAchievementReward(tier.Type, tier.Tier, card);
        }
    }
}