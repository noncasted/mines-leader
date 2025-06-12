using Common.Network.Common;
using Internal;

namespace Common.Network
{
    public static class SessionServicesExtensions
    {
        public static IScopeBuilder AddSessionServices(this IScopeBuilder builder)
        {
            AddEntityServices();
            AddEntityCommands();
            
            AddConnectionServices();
            
            AddUserServices();
            AddUserCommands();
            
            return builder;

            void AddEntityServices()
            {
                builder.Register<NetworkObjectsCollection>()
                    .As<INetworkObjectsCollection>();
                
                builder.Register<NetworkEntityCollection>()
                    .As<INetworkEntitiesCollection>();

                builder.Register<NetworkEntitiesIds>()
                    .As<INetworkEntityIds>();

                builder.Register<NetworkEntityFactory>()
                    .As<INetworkEntityFactory>();

                builder.Register<NetworkCommandsCollection>()
                    .AsSelfResolvable()
                    .As<INetworkCommandsCollection>();

                builder.Register<NetworkPropertiesUpdatesCollector>()
                    .As<IScopeSetup>();

                builder.Register<NetworkEntityDestroyer>()
                    .As<INetworkEntityDestroyer>();
            }

            void AddEntityCommands()
            {
                builder.RegisterCommand<EntityCreatedCommand>();
                builder.RegisterCommand<EntityDestroyedCommand>();
                builder.RegisterCommand<EntityPropertyUpdateCommand>();
                builder.RegisterCommand<PlayerDisconnectedCommand>();
                builder.RegisterCommand<EntityEventCommand>();
            }

            void AddConnectionServices()
            {
                builder.Register<NetworkConnection>()
                    .As<INetworkConnection>();

                builder.Register<NetworkReceiver>()
                    .As<INetworkReceiver>();

                builder.Register<NetworkSender>()
                    .As<INetworkSender>();

                builder.Register<NetworkSession>()
                    .As<INetworkSession>();
                
                builder.Register<NetworkSessionCallbacks>()
                    .As<INetworkSessionCallbacks>();

                builder.Register<NetworkCommandsDispatcher>()
                    .As<INetworkCommandsDispatcher>();

                builder.Register<NetworkResponsesDispatcher>()
                    .As<INetworkResponsesDispatcher>();
            }

            void AddUserServices()
            {
                builder.Register<NetworkUsersCollection>()
                    .As<INetworkUsersCollection>();
            }

            void AddUserCommands()
            {
                builder.RegisterCommand<LocalUserUpdateCommand>();
                builder.RegisterCommand<RemoteUserUpdateCommand>();
            }
        }
    }
}