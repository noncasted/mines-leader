using System;
using Cysharp.Threading.Tasks;
using Global.Backend;
using Shared;
using UnityEngine;

namespace Meta
{
    public static class BackendEndpoints
    {
        public static UniTask<SharedBackendUserSignUp.Response> SignUp(this IMetaBackend backend, string name)
        {
            return backend.Post<SharedBackendUserSignUp.Response, SharedBackendUserSignUp.Request>(
                    SharedBackendUserSignUp.Endpoint,
                    new SharedBackendUserSignUp.Request()
                );
        }

        public static UniTask<SharedBackendUserLogin.Response> LogIn(this IMetaBackend backend, Guid id)
        {
            return backend.Post<SharedBackendUserLogin.Response, SharedBackendUserLogin.Request>(
                    SharedBackendUserLogin.Endpoint,
                    new SharedBackendUserLogin.Request()
                    {
                        Id = id
                    }
                );
        }

        public static UniTask SearchGame(this IMetaBackend backend, GameMatchType type)
        {
            return backend.ExecuteCommand(new SharedMatchmaking.SearchMatch()
                    {
                        Type = type
                    }
                );
        }

        public static UniTask CancelSearch(this IMetaBackend backend)
        {
            return backend.ExecuteCommand(new SharedMatchmaking.CancelSearch());
        }

        public static UniTask CreateGame(this IMetaBackend backend)
        {
            return backend.ExecuteCommand(new SharedMatchmaking.Create());
        }

        public static UniTask CreateGameWithBot(
            this IMetaBackend backend,
            GameMatchType type,
            AgentMatchFixture fixture = null)
        {
            return backend.ExecuteCommand(new SharedMatchmaking.CreateWithBot()
                    {
                        Type = type,
                        Fixture = fixture
                    }
                );
        }

        public static UniTask SearchLobby(this IMetaBackend backend)
        {
            return backend.ExecuteCommand(new SharedMatchmaking.SearchLobby());
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

            if (response.HasError == true)
                Debug.LogError($"Claim achievement reward failed: {response.Message}");

            return response.HasError == false;
        }

        public static async UniTask ExecuteCommand<TRequest>(this IMetaBackend backend, TRequest request)
            where TRequest : INetworkContext
        {
            var response = await backend.Connection.Writer.WriteRequest<EmptyResponse>(request);

            if (response.HasError == true)
                Debug.LogError($"Request {typeof(TRequest).Name} executed with error: {response.Message}");
        }

        private static UniTask<TResponse> Post<TResponse, TRequest>(
            this IMetaBackend backend,
            string uri,
            TRequest body)
        {
            var backendUrl = backend.Client.Options.Url + uri;
            return backend.Client.PostJson<TResponse, TRequest>(backend.Lifetime, backendUrl, body);
        }
    }
}