using UnityEngine;

namespace GamePlay.Cards
{
    public interface ICardTransform
    {
        Vector2 Position { get; }
        Vector2 Scale { get; }
        float HandForce { get; }
        float Rotation { get; }
        
        void SetPosition(Vector2 position);
        void SetScale(Vector2 scale);
        void SetRotation(float angle);
        void SetHandForce(float force);
    }
}