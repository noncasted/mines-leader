using System;
using Cysharp.Threading.Tasks;
using Shared;
using UnityEngine;

namespace Meta
{
    public static class BackendEndpoints
    {
        // Queues the player; the result comes later as a MatchResult projection with this requestId.
        public static UniTask SearchGame(this IMetaBackend backend, GameMatchType type, Guid requestId)
        {
            return backend.ExecuteCommand(new SharedMatchmaking.SearchMatch()
            {
                Type = type,
                RequestId = requestId
            });
        }

        public static UniTask CancelSearch(this IMetaBackend backend)
        {
            return backend.ExecuteCommand(new SharedMatchmaking.CancelSearch());
        }

        // The session is created inside the command and comes as its response.
        public static UniTask<SharedMatchmaking.MatchResult> CreateGame(this IMetaBackend backend)
        {
            return backend.Connection.Writer.WriteRequest<SharedMatchmaking.MatchResult>(
                new SharedMatchmaking.Create());
        }

        public static UniTask<SharedMatchmaking.MatchResult> CreateGameWithBot(
            this IMetaBackend backend,
            GameMatchType type,
            AgentMatchFixture fixture = null)
        {
            return backend.Connection.Writer.WriteRequest<SharedMatchmaking.MatchResult>(
                new SharedMatchmaking.CreateWithBot()
                {
                    Type = type,
                    Fixture = fixture
                });
        }

        public static UniTask<SharedMatchmaking.LobbyResult> SearchLobby(this IMetaBackend backend)
        {
            return backend.Connection.Writer.WriteRequest<SharedMatchmaking.LobbyResult>(
                new SharedMatchmaking.SearchLobby());
        }

        public static UniTask<SharedBackendUser.MatchHistoryResponse> GetMatchHistory(
            this IMetaBackend backend,
            int count)
        {
            return backend.Connection.Writer.WriteRequest<SharedBackendUser.MatchHistoryResponse>(
                new SharedBackendUser.MatchHistoryRequest { Count = count });
        }

        public static UniTask<SharedBackendUser.MatchDetailsResponse> GetMatchDetails(
            this IMetaBackend backend,
            Guid matchId)
        {
            return backend.Connection.Writer.WriteRequest<SharedBackendUser.MatchDetailsResponse>(
                new SharedBackendUser.MatchDetailsRequest { MatchId = matchId });
        }

        public static UniTask<SharedBackendUser.AchievementRewardOptionsResponse> GetAchievementRewardOptions(
            this IMetaBackend backend,
            InGameAchievementType type,
            int tier)
        {
            return backend.Connection.Writer.WriteRequest<SharedBackendUser.AchievementRewardOptionsResponse>(
                new SharedBackendUser.AchievementRewardOptionsRequest
                {
                    Type = type,
                    Tier = tier
                });
        }

        public static async UniTask<bool> ClaimAchievementReward(
            this IMetaBackend backend,
            InGameAchievementType type,
            int tier,
            CardType card)
        {
            var response = await backend.Connection.Writer.WriteRequest<EmptyResponse>(
                new SharedBackendUser.ClaimAchievementRewardRequest
                {
                    Type = type,
                    Tier = tier,
                    Card = card
                });

            // A reward the rules refuse is EmptyResponse.Fail; a failed command throws (IConnectionWriter.WriteRequest).
            if (response.HasError == true)
                Debug.LogError($"Claim achievement reward failed: {response.Message}");

            return response.HasError == false;
        }

        // Throws when the command fails on the server (NetworkRequestException), on a timeout or a lost connection
        // (see IConnectionWriter.WriteRequest), and on a refusal (EmptyResponse.Fail): the caller must not go on
        // waiting for a result the server will not send.
        public static async UniTask ExecuteCommand<TRequest>(this IMetaBackend backend, TRequest request)
            where TRequest : INetworkContext
        {
            var response = await backend.Connection.Writer.WriteRequest<EmptyResponse>(request);

            if (response.HasError == true)
                throw new InvalidOperationException(
                    $"Request {ErrorResponse.Describe(request)} is refused by the server: {response.Message}");
        }
    }
}