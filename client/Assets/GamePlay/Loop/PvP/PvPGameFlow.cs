using System;
using System.Collections.Generic;
using Common.Network;
using Cysharp.Threading.Tasks;
using GamePlay.Players;
using Internal;

namespace GamePlay.Loop
{
    public class PvPGameFlow : NetworkService, IGameFlow
    {
        public PvPGameFlow(
            IGameContext context,
            IGameRound gameRound,
            INetworkUsersCollection sessionUsers)
        {
            _context = context;
            _gameRound = gameRound;
            _sessionUsers = sessionUsers;
        }

        private readonly IGameContext _context;
        private readonly IGameRound _gameRound;
        private readonly INetworkUsersCollection _sessionUsers;
        private readonly UniTaskCompletionSource<GameResult> _completion = new();

        public override void OnStarted(IReadOnlyLifetime lifetime)
        {
            Events.GetEvent<GameFlowEvents.Lose>().Advise(lifetime, context =>
            {
                if (context.PlayerId == _context.Self.Id)
                    return;
                
                OnWin(_context.Self);
            });
        }

        public async UniTask<GameResult> Execute(IReadOnlyLifetime lifetime)
        {
           var flowLifetime = lifetime.Child();

            var player = _context.Self;
            var hand = player.Hand;
            var deck = player.Deck;
            var gameOptions = _context.Options;

            var setupTasks = new List<UniTask>();

            for (var i = 0; i < gameOptions.RequiredCardsInHand; i++)
            {
                setupTasks.Add(deck.DrawCard(flowLifetime));
                await UniTask.Delay(TimeSpan.FromSeconds(0.3f));
            }

            await UniTask.WhenAll(setupTasks);

            CardLoop().Forget();

            if (_sessionUsers.Local.Index == 1)
                _gameRound.Start();

            var result = await _completion.Task;

            flowLifetime.Terminate();

            return result;
            
            async UniTask CardLoop()
            {
                while (flowLifetime.IsTerminated == false)
                {
                    while (hand.Entries.Count < gameOptions.RequiredCardsInHand)
                        await deck.DrawCard(flowLifetime);

                    await UniTask.WaitUntil(
                        () => hand.Entries.Count < gameOptions.RequiredCardsInHand,
                        cancellationToken: flowLifetime.Token);
                }
            }
        }

        public void OnLose(IGamePlayer player)
        {
            Events.Send(new GameFlowEvents.Lose(player.Id));
            
            _completion.TrySetResult(new GameResult()
            {
                Type = GameResultType.Lose
            });
        }

        public void OnWin(IGamePlayer player)
        {
            _completion.TrySetResult(new GameResult()
            {
                Type = GameResultType.Win
            });
        }

        public void OnLeave()
        {
            Events.Send(new GameFlowEvents.Lose(_context.Self.Id));
            
            _completion.TrySetResult(new GameResult()
            {
                Type = GameResultType.Leave
            });
        }
    }
}