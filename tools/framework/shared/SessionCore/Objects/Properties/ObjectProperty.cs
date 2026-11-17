using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Session
{
    public interface IObjectProperty
    {
        int Id { get; }
        int Version { get; }
        byte[] RawValue { get; }

        void Construct(IPropertyUpdateSender updateSender, int objectId);

        // Whether a write of this user's client is accepted (SetPropertyCommand asks before Update).
        bool CanBeWrittenBy(IUser user);
        void Update(byte[] value);
    }

    public class ObjectProperty : IObjectProperty
    {
        public ObjectProperty(int id, byte[] rawValue)
        {
            Id = id;
            _rawValue = rawValue;
        }

        private byte[] _rawValue;
        private int _objectId;
        private int _version;
        private IPropertyUpdateSender _updateSender;
        private Func<IUser, bool> _writerCheck;

        public int Id { get; }
        public int Version => _version;
        public byte[] RawValue => _rawValue;

        public void Construct(IPropertyUpdateSender updateSender, int objectId)
        {
            _objectId = objectId;
            _updateSender = updateSender;
        }

        // A property has one writer. Without a restriction any client writes it (entity properties, the scheme
        // of the location); a service restricts its properties to the writer named in its contract.
        public void RestrictWriter(Func<IUser, bool> check)
        {
            if (_writerCheck != null)
                throw new InvalidOperationException($"Property {Id} already has a writer restriction");

            _writerCheck = check;
        }

        public bool CanBeWrittenBy(IUser user)
        {
            return _writerCheck == null || _writerCheck(user);
        }

        // A write of a client (SetPropertyCommand).
        public virtual void Update(byte[] value)
        {
            SetRaw(value);
        }

        // A write of the backend itself: the raw value goes to the subscribers.
        public void Set(byte[] value)
        {
            SetRaw(value);
            Push();
        }

        public void Push()
        {
            _updateSender.Send(_objectId, this);
        }

        protected void SetRaw(byte[] value)
        {
            _rawValue = value;
            _version++;
        }
    }
}
