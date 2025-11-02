using Common.Network;
using Cysharp.Threading.Tasks;
using Internal;
using Meta;
using Shared;

namespace GamePlay.Loop
{
    public class RematchCommands
    {
        public class Failure : OneWayCommand<RematchContexts.Failure>
        {
            public Failure(IRematchAwaiter awaiter)
            {
                _awaiter = awaiter;
            }

            private readonly IRematchAwaiter _awaiter;

            protected override void Execute(IReadOnlyLifetime lifetime, RematchContexts.Failure context)
            {
                _awaiter.OnFailure();
            }
        }

        public class Success : OneWayCommand<RematchContexts.Success>
        {
            public Success(IRematchAwaiter awaiter)
            {
                _awaiter = awaiter;
            }

            private readonly IRematchAwaiter _awaiter;

            protected override void Execute(IReadOnlyLifetime lifetime, RematchContexts.Success context)
            {
                _awaiter.OnSuccess(context);
            }
        }
    }

    public interface IRematchAwaiter
    {
        UniTask<IGameEndTransition> Await(IReadOnlyLifetime lifetime);

        void OnFailure();
        void OnSuccess(RematchContexts.Success data);
    }

    public class RematchAwaiter : IRematchAwaiter
    {
        private readonly UniTaskCompletionSource<IGameEndTransition> _completion = new();

        public UniTask<IGameEndTransition> Await(IReadOnlyLifetime lifetime)
        {
            lifetime.Listen(() => _completion.TrySetResult(new GameEndTransition.Exit()));

            return _completion.Task;
        }

        public void OnFailure()
        {
            _completion.TrySetResult(new GameEndTransition.Exit());
        }

        public void OnSuccess(RematchContexts.Success data)
        {
            _completion.TrySetResult(new GameEndTransition.Rematch()
                {
                    NewSession = new SessionData(data.ServerUrl, data.SessionId)
                }
            );
        }
    }
}