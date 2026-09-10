using UnityEngine;

namespace Meta
{
    public interface IModifierDefinition
    {
        string Type { get; }
        string Name { get; }
        string Description { get; }
        Sprite Image { get; }

        /// <summary>
        /// Описание с подставленным размером эффекта: направление ("+" или "-") зашито
        /// в текст, поэтому в него уходит модуль значения модификатора.
        /// </summary>
        string Describe(float value);
    }

    public class ModifierDefinition : IModifierDefinition
    {
        public ModifierDefinition(string type, string name, string description, Sprite image)
        {
            Type = type;
            Name = name;
            Description = description;
            Image = image;
        }

        public string Type { get; }
        public string Name { get; }
        public string Description { get; }
        public Sprite Image { get; }

        public string Describe(float value)
        {
            var amount = Mathf.RoundToInt(Mathf.Abs(value));

            return string.Format(Description, amount);
        }
    }
}