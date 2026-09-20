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

    // Двигается вьюха, а не корень карты: корень держит скоуп и порядок сортировки.
    public class CardTransform : ICardTransform
    {
        public CardTransform(GameCardBindings bindings)
        {
            _transform = bindings.View.Transform;
        }

        private readonly Transform _transform;

        private float _handForce;

        public Vector2 Position => _transform.position;
        public Vector2 Scale => _transform.localScale;

        public float HandForce => _handForce;
        public float Rotation => _transform.rotation.eulerAngles.z;

        public void SetPosition(Vector2 position)
        {
            _transform.position = position;
        }

        public void SetScale(Vector2 scale)
        {
            _transform.localScale = scale;
        }

        public void SetRotation(float angle)
        {
            _transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        public void SetHandForce(float force)
        {
            _handForce = force;
        }
    }
}
