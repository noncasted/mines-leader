using GamePlay.Services;
using Internal;
using UnityEngine;

namespace GamePlay.Cards
{
    public enum CardResource
    {
        Health,
        Moves,
        Mana,
        Cards
    }

    public interface ICardResourceFloatingText
    {
        /// <summary>
        /// Delay between lines when a card changes more than one parameter at once.
        /// </summary>
        float StepDelay { get; }

        void Show(Vector2 position, CardResource resource, int amount, float delay = 0f);
        void Show(Vector2 position, CardResource resource, string text, bool isPositive, float delay = 0f);
    }

    /// <summary>
    /// Shows resource gains and losses (hp, moves, mana) above the place
    /// where the dice or the coin has landed.
    /// </summary>
    public class CardResourceFloatingText : ICardResourceFloatingText
    {
        public CardResourceFloatingText(IGameFloatingText floatingText)
        {
            _floatingText = floatingText;
        }

        private readonly IGameFloatingText _floatingText;

        public float StepDelay => 0.25f;

        public void Show(Vector2 position, CardResource resource, int amount, float delay = 0f)
        {
            if (amount == 0)
                return;

            var text = amount > 0 ? $"+{amount}" : amount.ToString();

            Show(position, resource, text, amount > 0, delay);
        }

        public void Show(Vector2 position, CardResource resource, string text, bool isPositive, float delay = 0f)
        {
            var color = isPositive == true ? Colors.Buffs.Add : Colors.Buffs.Remove;

            _floatingText.Create(position)
                         .WithText(text, color)
                         .WithIcon(GetIcon(resource))
                         .WithDelay(delay)
                         .RunDetached();
        }

        private static Sprite GetIcon(CardResource resource)
        {
            return resource switch
            {
                CardResource.Health => Sprites.GameUI.HealthLargeBaseFull,
                CardResource.Moves => Sprites.GameUI.TurnLargeBaseFull,
                CardResource.Mana => Sprites.GameUI.ManaLargeBaseFull,
                CardResource.Cards => Sprites.GameUI.Card,
                _ => null
            };
        }
    }
}
