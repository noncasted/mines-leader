using System;
using UnityEngine;

namespace Internal
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class CurveRangeAttribute : PropertyAttribute
    {
        public Vector2 Min { get; }
        public Vector2 Max { get; }
        public bool HasRange { get; }

        public CurveRangeAttribute()
        {
            Min = Vector2.zero;
            Max = Vector2.zero;
            HasRange = false;
        }

        public CurveRangeAttribute(float minX, float minY, float maxX, float maxY)
        {
            Min = new Vector2(minX, minY);
            Max = new Vector2(maxX, maxY);
            HasRange = true;
        }
    }
}
