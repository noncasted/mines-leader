using System;
using UnityEngine;

namespace Internal
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class MinMaxSliderAttribute : PropertyAttribute
    {
        public float MinValue { get; }
        public float MaxValue { get; }

        public MinMaxSliderAttribute(float minValue, float maxValue)
        {
            MinValue = minValue;
            MaxValue = maxValue;
        }
    }
}
