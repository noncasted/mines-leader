using UnityEngine;

namespace Tools.PrefabBuilder
{
    public static class TransformBuilderExtensions
    {
        public static PrefabBuilder WithPosition(this PrefabBuilder builder, float x, float y, float z)
        {
            builder.GameObject.transform.localPosition = new Vector3(x, y, z);
            return builder;
        }

        public static PrefabBuilder WithScale(this PrefabBuilder builder, float x, float y, float z)
        {
            builder.GameObject.transform.localScale = new Vector3(x, y, z);
            return builder;
        }

        public static PrefabBuilder WithRotation(this PrefabBuilder builder, float x, float y, float z)
        {
            builder.GameObject.transform.localEulerAngles = new Vector3(x, y, z);
            return builder;
        }
    }
}