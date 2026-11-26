using System.Collections.Generic;
using Shared;

namespace Game.Session
{
    public class SubscribersUpdateSender : IPropertyUpdateSender
    {
        public SubscribersUpdateSender(IReadOnlyList<IUser> subscribers)
        {
            _subscribers = subscribers;
        }

        private readonly IReadOnlyList<IUser> _subscribers;

        public void Send(int objectId, IObjectProperty property)
        {
            var context = new SharedSessionObject.PropertyUpdate()
            {
                ObjectId = objectId,
                PropertyId = property.Id,
                Value = property.RawValue,
                Version = property.Version
            };

            foreach (var user in _subscribers)
                user.Send(context);
        }
    }
}
