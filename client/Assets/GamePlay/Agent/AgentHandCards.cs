#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using GamePlay.Cards;
using GamePlay.Loop;
using Shared;

namespace GamePlay.Agent
{
    // Cards of the own hand by id or by type: the client hand first, the last observation when the hand
    // has not caught up with the server yet.
    public class AgentHandCards
    {
        public AgentHandCards(IGameContext gameContext, AgentObservations observations)
        {
            _gameContext = gameContext;
            _observations = observations;
        }

        private readonly IGameContext _gameContext;
        private readonly AgentObservations _observations;

        public bool TryFindByType(CardType type, out Guid cardId)
        {
            foreach (ICard card in _gameContext.Self.Hand.Entries)
            {
                if (card.Type != type)
                    continue;

                cardId = card.Id;
                return true;
            }

            var observed = _observations.Last?.Self.Hand;

            if (observed != null)
            {
                foreach (var card in observed)
                {
                    if (string.Equals(card.Type, type.ToString(), StringComparison.Ordinal) == false)
                        continue;

                    cardId = card.Id;
                    return true;
                }
            }

            cardId = Guid.Empty;
            return false;
        }

        public bool TryGetType(Guid cardId, out CardType type)
        {
            foreach (ICard card in _gameContext.Self.Hand.Entries)
            {
                if (card.Id != cardId)
                    continue;

                type = card.Type;
                return true;
            }

            var observed = _observations.Last?.Self.Hand;

            if (observed != null)
            {
                foreach (var card in observed)
                {
                    if (card.Id == cardId)
                        return Enum.TryParse(card.Type, out type);
                }
            }

            type = default;
            return false;
        }
    }
}
#endif
