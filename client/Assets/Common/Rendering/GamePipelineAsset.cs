using UnityEngine;
using UnityEngine.Rendering;

namespace Rendering
{
    [CreateAssetMenu(menuName = "Rendering/Game Pipeline", fileName = "GamePipeline")]
    public class GamePipelineAsset : RenderPipelineAsset<GamePipeline>
    {
        [SerializeField] private Material _spriteMaterial;
        [SerializeField] private bool _useSrpBatcher = true;

        public bool UseSrpBatcher => _useSrpBatcher;

        public override Material default2DMaterial => _spriteMaterial;

        protected override RenderPipeline CreatePipeline()
        {
            return new GamePipeline(this);
        }
    }
}
