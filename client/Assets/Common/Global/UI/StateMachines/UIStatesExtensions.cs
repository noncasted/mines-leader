using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Global.UI
{
    public static class UIStatesExtensions
    {
        public static IUIStateHandle EnterChild(
            this IUIStateMachine stateMachine,
            IReadOnlyLifetime lifetime,
            IUIState parent,
            IUIState state)
        {
            return stateMachine.CreateChild(lifetime, parent, state).TryEnter();
        }

        public static async UniTask ProcessChild(
            this IUIStateMachine stateMachine,
            IReadOnlyLifetime lifetime,
            IUIState parent,
            IUIState state)
        {
            var handle = stateMachine.CreateChild(lifetime, parent, state);
            await handle.TryProcess();
            handle.Exit();
        }

        public static async UniTask ProcessChild(
            this IUIStateMachine stateMachine,
            IReadOnlyLifetime lifetime,
            IUIState parent,
            IUIState state,
            Func<IUIStateHandle, UniTask> action)
        {
            var handle = stateMachine.CreateChild(lifetime, parent, state);
            await action.Invoke(handle);
            handle.Exit();
        }

        public static async UniTask<T> ProcessChild<T>(
            this IUIStateMachine stateMachine,
            IReadOnlyLifetime lifetime,
            IUIState parent,
            IUIState state,
            Func<IUIStateHandle, UniTask<T>> action)
        {
            var handle = stateMachine.CreateChild(lifetime, parent, state);
            var result = await action.Invoke(handle);
            handle.Exit();

            return result;
        }

        public static UniTask ProcessStack(
            this IUIStateMachine stateMachine,
            IReadOnlyLifetime lifetime,
            IUIState parent,
            IUIState state)
        {
            return stateMachine.CreateStackChild(lifetime, parent, state).TryProcess();
        }

        public static async UniTask ProcessStack(
            this IUIStateMachine stateMachine,
            IReadOnlyLifetime lifetime,
            IUIState parent,
            IUIState state,
            Func<IUIStateHandle, UniTask> action)
        {
            var handle = stateMachine.CreateStackChild(lifetime, parent, state);
            await action.Invoke(handle);
            handle.Exit();
        }

        public static async UniTask<T> ProcessStack<T>(
            this IUIStateMachine stateMachine,
            IReadOnlyLifetime lifetime,
            IUIState parent,
            IUIState state,
            Func<IUIStateHandle, UniTask<T>> action)
        {
            var handle = stateMachine.CreateStackChild(lifetime, parent, state);
            var result = await action.Invoke(handle);
            handle.Exit();
            return result;
        }

        public static UniTask Process(
            this IUIStateMachine stateMachine,
            IReadOnlyLifetime lifetime,
            IUIState parent,
            IUIState state)
        {
            var handle = stateMachine.CreateChild(lifetime, parent, state);
            return handle.TryProcess();
        }

        public static async UniTask TryProcess(this IUIStateHandle handle)
        {
            switch (handle.State)
            {
                case IUIStateEnterHandler handler:
                    handler.OnEntered(handle);
                    await handle.Completion.Task;
                    handle.Exit();
                    break;
                case IUIStateAsyncEnterHandler handler:
                    await handler.OnEntered(handle);
                    handle.Exit();
                    break;
            }
        }

        public static IUIStateHandle TryEnter(this IUIStateHandle handle)
        {
            switch (handle.State)
            {
                case IUIStateEnterHandler handler:
                    handler.OnEntered(handle);
                    break;
                case IUIStateAsyncEnterHandler handler:
                    handler.OnEntered(handle);
                    break;
            }

            return handle;
        }
    }
}