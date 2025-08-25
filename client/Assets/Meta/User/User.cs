using System;
using Shared;

namespace Meta
{
    public interface IUser
    {
        Guid Id { get; }
        CharacterType Character { get; }

        void Init(Guid id);
    }
    
    public class User : IUser
    {
        private Guid _id;

        public Guid Id => _id;
        public CharacterType Character => CharacterType.BIBA;

        public void Init(Guid id)
        {
            _id = id;
        }
    }
}