using System;

namespace Internal
{
    public static class ViewablePropertyExtensions
    {
        // The value is clamped before it is set: the listeners never see one over the maximum.
        public static int AddClampMax(this ViewableProperty<int> property, int add, int maxValue)
        {
            var value = Math.Min(property.Value + add, maxValue);
            property.Set(value);
            return value;
        }

        public static int Increase(this ViewableProperty<int> property)
        {
            var value = property.Value + 1;
            property.Set(value);
            return value;
        }

        public static int IncreaseClampMax(this ViewableProperty<int> property, int maxValue)
        {
            return property.AddClampMax(1, maxValue);
        }

        public static int Decrease(this ViewableProperty<int> property)
        {
            var value = property.Value - 1;
            property.Set(value);
            return value;
        }

        public static bool IsZero(this ViewableProperty<int> property)
        {
            return property.Value == 0;
        }

        public static void Add(this ViewableProperty<int> property, int amount)
        {
            var currentValue = property.Value;
            property.Set(currentValue + amount);
        }

        public static void Remove(this ViewableProperty<int> property, int amount)
        {
            var currentValue = property.Value;
            property.Set(currentValue - amount);
        }

        public static bool IsZero(this IViewableProperty<int> property)
        {
            return property.Value == 0;
        }
    }
}