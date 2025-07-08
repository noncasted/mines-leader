using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GamePlay.Players;
using Internal;

namespace GamePlay.Loop
{
    public class SingleGameFlow : IGameFlow
    {
        public SingleGameFlow(IGameContext context, IGameRound gameRound)
        {
            _context = context;
            _gameRound = gameRound;
        }

        private readonly IGameContext _context;
        private readonly IGameRound _gameRound;
        private readonly UniTaskCompletionSource<GameResult> _completion = new();

        private ILifetime _flowLifetime;

        public async UniTask<GameResult> Execute(IReadOnlyLifetime lifetime)
        {
            _flowLifetime = lifetime.Child();

            var player = _context.Self;
            var hand = player.Hand;
            var deck = player.Deck;
            var gameOptions = _context.Options;

            var setupTasks = new List<UniTask>();

            for (var i = 0; i < gameOptions.RequiredCardsInHand; i++)
            {
                setupTasks.Add(deck.DrawCard(_flowLifetime));
                await UniTask.Delay(TimeSpan.FromSeconds(0.3f));
            }

            await UniTask.WhenAll(setupTasks);

            _gameRound.Start();

            while (_flowLifetime.IsTerminated == false)
            {
                while (hand.Entries.Count < gameOptions.RequiredCardsInHand)
                    await deck.DrawCard(_flowLifetime);

                await UniTask.WaitUntil(
                    () => hand.Entries.Count < gameOptions.RequiredCardsInHand,
                    cancellationToken: _flowLifetime.Token);
            }

            return await _completion.Task;
        }

        public void OnLose(IGamePlayer player)
        {
            _flowLifetime.Terminate();
        }

        public void OnWin(IGamePlayer player)
        {
            _flowLifetime.Terminate();
        }

        public void OnLeave()
        {
        }
    }
}