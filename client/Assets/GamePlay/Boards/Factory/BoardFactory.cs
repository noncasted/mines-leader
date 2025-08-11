using Cysharp.Threading.Tasks;
using GamePlay.Players;
using Internal;
using UnityEngine;

namespace GamePlay.Boards
{
    public interface IBoardFactory
    {
        UniTask Create(PlayerBuildContext context);
    }
    
    [DisallowMultipleComponent]
    public class BoardFactory : MonoBehaviour, IBoardFactory
    {
        [SerializeField] private Board _prefab;
        [SerializeField] private Transform _parent;
        
        public UniTask Create(PlayerBuildContext context)
        {
            var board = Instantiate(_prefab, _parent);
            board.transform.localPosition = Vector3.zero;

            context.Builder.RegisterComponent(board)
                .As<IBoard>()
                .As<IScopeSetup>();

            return UniTask.CompletedTask;
        }
    }
}