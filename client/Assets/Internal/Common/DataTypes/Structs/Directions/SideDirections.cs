using UnityEngine;

namespace Internal
{
    public enum Direction2
    {
        Forward,
        Backward,
    }
    
    public enum Direction4
    {
        Up,
        Right,
        Down,
        Left
    }
    
    public enum Direction8
    {
        Up,
        Right,
        Down,
        Left
    }

    public static class SideExtensions
    {
        public static Direction4 ToDirection4(this Vector2 vector)
        {
            if (vector.x > 0)
            {
                return Direction4.Right;
            }

            if (vector.x < 0)
            {
                return Direction4.Left;
            }

            if (vector.y > 0)
            {
                return Direction4.Up;
            }

            return Direction4.Down;
        }
        
        public static Direction8 ToDirection8(this Vector2 vector)
        {
            if (vector.x > 0)
            {
                return Direction8.Right;
            }

            if (vector.x < 0)
            {
                return Direction8.Left;
            }

            if (vector.y > 0)
            {
                return Direction8.Up;
            }

            return Direction8.Down;
        }
    }
}