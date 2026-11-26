using System;
using Shared;

namespace Game.Session
{
    // Runs the commands of the messages of a user, in the session queue of its host (one at a time). Every request is
    // answered: the result of its command, or ErrorResponse when there is no command for its type or the command
    // throws (the client turns it into an exception instead of waiting forever). A failed one-way command is logged
    // with its context; the session goes on (known races of entity ids, see the commands).
    public interface ISessionCommandRunner
    {
        void RunOneWay(IUser user, INetworkContext context);
        INetworkContext RunRequest(IUser user, INetworkContext context, int requestId);
    }

    public class SessionCommandRunner : ISessionCommandRunner
    {
        public SessionCommandRunner(ICommandsCollection commands, ISessionLog log)
        {
            _commands = commands;
            _log = log;
        }

        private readonly ICommandsCollection _commands;
        private readonly ISessionLog _log;

        public void RunOneWay(IUser user, INetworkContext context)
        {
            var type = context.GetType();

            if (_commands.OneWay.TryGetValue(type, out var command) == false)
            {
                _log.Error($"[Session] [Command] No one-way command for {type.FullName} from user {user.Index} {user.Id}");
                return;
            }

            try
            {
                command.Execute(user, context);
            }
            catch (Exception e)
            {
                _log.Error($"[Session] [Command] One-way command {type.FullName} of user {user.Index} {user.Id} failed", e);
            }
        }

        public INetworkContext RunRequest(IUser user, INetworkContext context, int requestId)
        {
            var type = context.GetType();

            if (_commands.Responsible.TryGetValue(type, out var command) == false)
            {
                _log.Error(
                    $"[Session] [Command] No request command for {type.FullName} from user {user.Index} {user.Id}, request {requestId}");

                return ErrorResponse.UnknownCommand(context);
            }

            try
            {
                return command.Execute(user, context);
            }
            catch (SessionRequestException e)
            {
                _log.Warning(
                    $"[Session] [Command] Request {type.FullName} of user {user.Index} {user.Id} refused ({e.Code}): {e.Message}, request {requestId}");

                return ErrorResponse.Create(e.Code, context, e);
            }
            catch (Exception e)
            {
                _log.Error(
                    $"[Session] [Command] Request command {type.FullName} of user {user.Index} {user.Id} failed, request {requestId}",
                    e);

                return ErrorResponse.Create(ErrorCode.Internal, context, e);
            }
        }
    }
}
