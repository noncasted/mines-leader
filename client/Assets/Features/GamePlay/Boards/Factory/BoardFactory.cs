using Common.Network;
using Cysharp.Threading.Tasks;
using GamePlay.Players;
using Internal;
using UnityEngine;

namespace GamePlay.Boards
{
    [DisallowMultipleComponent]
    public class BoardFactory : MonoBehaviour, IBoardFactory
    {
        [SerializeField] private Board _prefab;
        [SerializeField] private Transform _parent;
        
        public UniTask CreateLocal(PlayerBuildContext context)
        {
            var board = Instantiate(_prefab, _parent);
            board.transform.localPosition = Vector3.zero;

            context.Builder.RegisterProperty<NetworkBoardCellsState>();
            
            context.Builder.RegisterComponent(board)
                .As<IBoard>()
                .As<IScopeSetup>();

            return UniTask.CompletedTask;
        }
        
        public UniTask CreateRemote(PlayerBuildContext context)
        {
            var board = Instantiate(_prefab, _parent);
            board.transform.localPosition = Vector3.zero;
            
            context.Builder.RegisterProperty<NetworkBoardCellsState>();

            context.Builder.RegisterComponent(board)
                .As<IBoard>()
                .As<IScopeSetup>();

            return UniTask.CompletedTask;
        }
    }
}