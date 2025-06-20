using System;
using System.Collections.Generic;
using Common.Network;
using Cysharp.Threading.Tasks;
using GamePlay.Players;
using Internal;

namespace GamePlay.Loop
{
    public class PvPGameFlow : IGameFlow
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

        private ILifetime _flowLifetime;

        public async UniTask Execute(IReadOnlyLifetime lifetime)
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
            
            CardLoop().Forget();
            
            if (_sessionUsers.Local.Index == 1)
                _gameRound.Start();

            async UniTask CardLoop()
            {
                while (_flowLifetime.IsTerminated == false)
                {
                    while (hand.Entries.Count < gameOptions.RequiredCardsInHand)
                        await deck.DrawCard(_flowLifetime);

                    await UniTask.WaitUntil(
                        () => hand.Entries.Count < gameOptions.RequiredCardsInHand,
                        cancellationToken: _flowLifetime.Token);
                }
            }
        }

        public void OnLose(IGamePlayer player)
        {
            _flowLifetime.Terminate();
        }

        public void OnWin(IGamePlayer player)
        {
            _flowLifetime.Terminate();
        }
    }
}