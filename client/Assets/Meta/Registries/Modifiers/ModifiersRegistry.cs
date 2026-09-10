using System;
using System.Collections.Generic;
using Internal;
using UnityEngine;

namespace Meta
{
    [Serializable]
    public class ModifiersInfoPayload
    {
        public ModifierInfoPayload[] buffs;
    }

    [Serializable]
    public class ModifierInfoPayload
    {
        public string type;
        public string name;
        public string description;
    }

    public interface IModifiersRegistry
    {
        IReadOnlyDictionary<string, IModifierDefinition> Entries { get; }
        bool TryGet(string key, out IModifierDefinition definition);
    }

    public class ModifiersRegistry : IModifiersRegistry
    {
        public ModifiersRegistry()
        {
            Load();
        }

        private readonly Dictionary<string, IModifierDefinition> _buffs = new();
        private readonly Dictionary<string, IModifierDefinition> _bySourceKey = new();

        public IReadOnlyDictionary<string, IModifierDefinition> Entries => _buffs;

        public bool TryGet(string key, out IModifierDefinition definition)
        {
            if (_buffs.TryGetValue(key, out definition))
                return true;

            return _bySourceKey.TryGetValue(key, out definition);
        }

        private void Load()
        {
            var textAsset = Resources.Load<TextAsset>("buffs-info");
            var payload = JsonUtility.FromJson<ModifiersInfoPayload>(textAsset.text);

            foreach (var entry in payload.buffs)
            {
                var definition = new ModifierDefinition(entry.type, entry.name, entry.description,
                    TypeToSprite(entry.type));
                _buffs[entry.type] = definition;
                _bySourceKey[TypeToSourceKey(entry.type)] = definition;
            }
        }

        private static Sprite TypeToSprite(string type)
        {
            return type switch
            {
                "BaseHealth" => Sprites.CardBuffs.BaseHealth,
                "BaseMoves" => Sprites.CardBuffs.BaseTurns,
                "BaseMana" => Sprites.CardBuffs.BaseMana,
                "Adrenaline" => Sprites.CardBuffs.Adrenaline,
                "BloodPact" => Sprites.CardBuffs.BloodPact,
                "CoinToss" => Sprites.CardBuffs.CoinToss,
                "CoinTossTails" => Sprites.CardBuffs.CoinToss,
                "DoubleOrNothing" => Sprites.CardBuffs.DoubleOrNothing,
                "Embargo" => Sprites.CardBuffs.Embargo,
                "Focus" => Sprites.CardBuffs.Focus,
                "GamblersRuin" => Sprites.CardBuffs.GamblersRuin,
                "Lockdown" => Sprites.CardBuffs.Lockdown,
                "ManaFountain" => Sprites.CardBuffs.ManaFountain,
                "ManaSurge" => Sprites.CardBuffs.ManaSurge,
                "Overclock" => Sprites.CardBuffs.Overclock,
                "PowerSurge" => Sprites.CardBuffs.PowerSurge,
                "SoulLink" => Sprites.CardBuffs.SoulLink,
                "TrebuchetAimer" => Sprites.CardBuffs.TrebuchetAimer,
                // Иконки может не быть — тогда баф всё равно попадает в список с именем
                // и описанием, а не роняет весь реестр вместе с HUD бафов.
                _ => null
            };
        }

        private static string TypeToSourceKey(string type)
        {
            return type switch
            {
                "BaseHealth" => "base_health",
                "BaseMoves" => "base_moves",
                "BaseMana" => "base_mana",
                "Adrenaline" => "adrenaline",
                "BloodPact" => "blood_pact",
                "CoinToss" => "cointoss",
                "CoinTossTails" => "cointoss_tails",
                "DoubleOrNothing" => "double_or_nothing",
                "Embargo" => "embargo",
                "Focus" => "focus",
                "GamblersRuin" => "gamblers_ruin",
                "Lockdown" => "lockdown",
                "ManaFountain" => "mana_fountain",
                "ManaSurge" => "mana_surge",
                "Overclock" => "overclock",
                "PowerSurge" => "powersurge",
                "SoulLink" => "soul_link",
                "Shield" => "shield",
                "TrebuchetAimer" => "trebuchet_aimer",
                _ => type
            };
        }
    }
}