using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Meta
{
    public interface IMetaConnectionAwaiter
    {
        UniTask CompleteTask { get; }
    }

    public class ConnectionCompletedCommand : OneWayCommand<SharedConnectionCompleted>, IMetaConnectionAwaiter
    {
        public ConnectionCompletedCommand()
        {
            _completionSource = new UniTaskCompletionSource();
        }

        private readonly UniTaskCompletionSource _completionSource;

        public UniTask CompleteTask => _completionSource.Task;

        protected override void Execute(IReadOnlyLifetime lifetime, SharedConnectionCompleted context)
        {
            _completionSource.TrySetResult();
        }
    }
}