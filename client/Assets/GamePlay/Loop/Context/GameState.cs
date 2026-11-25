using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace GamePlay.Loop
{
    public enum GameStateType
    {
        WaitingFoPlayers,
        Active,
        Completed
    }

    public interface IGameState
    {
        IViewableProperty<GameStateType> Value { get; }
        IViewableProperty<MatchCompletedData> CompletedData { get; }

        void Set(GameStateType type);
        void SetWinner(GameCompletedRecord record);
        UniTask<MatchCompletedData> WaitCompletion(IReadOnlyLifetime lifetime);
        void OnLeave();
        void OnFailed();
    }

    public class GameState : IGameState
    {
        public GameState(IGameContext context)
        {
            _context = context;
        }

        private readonly IGameContext _context;
        private readonly UniTaskCompletionSource<MatchCompletedData> _completion = new();

        private readonly ViewableProperty<GameStateType> _value = new(GameStateType.WaitingFoPlayers);
        private readonly ViewableProperty<MatchCompletedData> _completed = new();

        public IViewableProperty<GameStateType> Value => _value;
        public IViewableProperty<MatchCompletedData> CompletedData => _completed;

        public void Set(GameStateType type)
        {
            _value.Set(type);
        }

        public void SetWinner(GameCompletedRecord record)
        {
            // No winner: the round ended without a result (the session was closed before anybody won), the backend
            // closes the match without results and there is nothing to show.
            if (record.Winner == Guid.Empty)
                return;

            var player = _context.GetPlayer(record.Winner);
            var self = SelectSelfResult(record);
            var isWin = player.Info.IsLocal == true;

            _completion.TrySetResult(new MatchCompletedData()
            {
                Type = isWin ? MatchResultType.Win : MatchResultType.Lose,
                Duration = record.Duration,
                RatingChange = self.RatingChange,
                CurrentRating = self.Rating,
                Stats = self.Stats,
                LoserId = isWin ? _context.Other.Id : _context.Self.Id,
                Boards = record.Boards
            });
        }

        private MatchPlayerResult SelectSelfResult(GameCompletedRecord record)
        {
            return record.Players.First(t => t.PlayerId == _context.Self.Id);
        }

        public async UniTask<MatchCompletedData> WaitCompletion(IReadOnlyLifetime lifetime)
        {
            var data = await _completion.Task;
            _completed.Set(data);
            return data;
        }

        public void OnLeave()
        {
            _completion.TrySetResult(new MatchCompletedData()
            {
                Type = MatchResultType.Leave
            });
        }

        public void OnFailed()
        {
            _completion.TrySetResult(new MatchCompletedData()
            {
                Type = MatchResultType.Failed
            });
        }
    }
}
