using System;
using UnityEngine;

namespace Shared
{
    public readonly struct Position : IEquatable<Position>
    {
        public Position(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public int x { get; }
        public int y { get; }

        public override bool Equals(object obj)
        {
            if (obj is Position other)
                return x == other.x && y == other.y;

            return false;
        }

        public bool Equals(Position other)
        {
            return x == other.x && y == other.y;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, y);
        }

        public static bool operator ==(Position left, Position right)
        {
            return (left.x == right.x && left.y == right.y);
        }

        public static bool operator !=(Position left, Position right)
        {
            return !(left == right);
        }

        public static Position operator +(Position left, Position right)
        {
            return new Position(left.x + right.x, left.y + right.y);
        }

        public static Position operator -(Position left, Position right)
        {
            return new Position(left.x - right.x, left.y - right.y);
        }

        public override string ToString()
        {
            return $"({x}, {y})";
        }

        public double DistanceTo(Position other)
        {
            return Math.Sqrt(Math.Pow(x - other.x, 2) + Math.Pow(y - other.y, 2));
        }
    }

    public static class PositionExtensions
    {
        public static Position ToPosition(this Vector2Int vector)
        {
            return new Position(vector.x, vector.y);
        }

        public static Vector2Int ToVector(this Position position)
        {
            return new Vector2Int(position.x, position.y);
        }
    }
}