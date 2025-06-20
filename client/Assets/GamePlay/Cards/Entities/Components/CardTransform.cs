using Internal;
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
    
    [DisallowMultipleComponent]
    public class CardTransform : MonoBehaviour, ICardTransform, IEntityComponent
    {
        private float _handForce;
        
        public Vector2 Position => transform.position;
        public Vector2 Scale => transform.localScale;

        public float HandForce => _handForce;
        public float Rotation => transform.rotation.eulerAngles.z;

        public void Register(IEntityBuilder builder)
        {
            builder.RegisterComponent(this)
                .As<ICardTransform>();
        }
        
        public void SetPosition(Vector2 position)
        {
            transform.position = position;
        }

        public void SetScale(Vector2 scale)
        {
            transform.localScale = scale;
        }

        public void SetRotation(float angle)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        public void SetHandForce(float force)
        {
            _handForce = force;
        }
    }
}