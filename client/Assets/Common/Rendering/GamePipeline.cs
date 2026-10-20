using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Rendering
{
    /// <summary>
    /// 2D без света: на камеру один проход, все очереди сразу, сортировка как у прозрачных
    /// (sorting layer → order in layer → кастомная ось). Постпроцессинг встанет между
    /// отрисовкой и выводом в таргет камеры.
    /// </summary>
    public class GamePipeline : RenderPipeline
    {
        public GamePipeline(GamePipelineAsset asset)
        {
            GraphicsSettings.useScriptableRenderPipelineBatching = asset.UseSrpBatcher;
        }

        private static readonly ShaderTagId UnlitPass = new("SRPDefaultUnlit");

        private static readonly int TimeId = Shader.PropertyToID("_Time");
        private static readonly int SinTimeId = Shader.PropertyToID("_SinTime");
        private static readonly int CosTimeId = Shader.PropertyToID("_CosTime");
        private static readonly int DeltaTimeId = Shader.PropertyToID("unity_DeltaTime");

        private readonly CommandBuffer _cmd = new() { name = "Game Pipeline" };

        protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
        {
            BeginContextRendering(context, cameras);
            SetTime();
            context.ExecuteCommandBuffer(_cmd);
            _cmd.Clear();

            foreach (var camera in cameras)
                RenderCamera(context, camera);

            EndContextRendering(context, cameras);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _cmd.Release();
        }

        private void RenderCamera(ScriptableRenderContext context, Camera camera)
        {
            BeginCameraRendering(context, camera);

#if UNITY_EDITOR
            if (camera.cameraType == CameraType.SceneView)
                ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif

            if (camera.TryGetCullingParameters(out var cullingParameters) == false)
            {
                EndCameraRendering(context, camera);
                return;
            }

            var culling = context.Cull(ref cullingParameters);
            context.SetupCameraProperties(camera);

            Clear(camera);

            var sorting = new SortingSettings(camera)
            {
                criteria = SortingCriteria.CommonTransparent,
                distanceMetric = DistanceMetric.CustomAxis,
                customAxis = Vector3.up
            };

            var drawing = new DrawingSettings(UnlitPass, sorting) { enableInstancing = true };
            var filtering = new FilteringSettings(RenderQueueRange.all);
            var rendererListParams = new RendererListParams(culling, drawing, filtering);
            _cmd.DrawRendererList(context.CreateRendererList(ref rendererListParams));

#if UNITY_EDITOR
            if (UnityEditor.Handles.ShouldRenderGizmos())
            {
                _cmd.DrawRendererList(context.CreateGizmoRendererList(camera, GizmoSubset.PreImageEffects));
                _cmd.DrawRendererList(context.CreateGizmoRendererList(camera, GizmoSubset.PostImageEffects));
            }
#endif

            context.ExecuteCommandBuffer(_cmd);
            _cmd.Clear();
            context.Submit();

            EndCameraRendering(context, camera);
        }

        private void Clear(Camera camera)
        {
            var flags = camera.clearFlags;

            if (flags == CameraClearFlags.Nothing)
                return;

            if (flags == CameraClearFlags.Depth)
            {
                _cmd.ClearRenderTarget(true, false, Color.clear);
                return;
            }

            // Скайбокса в 2D нет: Skybox чистится цветом фона, как это делал URP 2D.
            // Экран всегда непрозрачный: у URP кадр шёл через HDR-буфер без альфы,
            // а прозрачный фон в WebGL просвечивает страницу.
            var color = QualitySettings.activeColorSpace == ColorSpace.Linear
                ? camera.backgroundColor.linear
                : camera.backgroundColor;

            if (camera.targetTexture == null)
                color.a = 1f;

            _cmd.ClearRenderTarget(true, true, color);
        }

        private void SetTime()
        {
#if UNITY_EDITOR
            var time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
#else
            var time = Time.time;
#endif
            var deltaTime = Time.deltaTime;
            var smoothDeltaTime = Time.smoothDeltaTime;

            _cmd.SetGlobalVector(TimeId, time * new Vector4(1f / 20f, 1f, 2f, 3f));
            _cmd.SetGlobalVector(SinTimeId, new Vector4(Mathf.Sin(time / 8f), Mathf.Sin(time / 4f), Mathf.Sin(time / 2f), Mathf.Sin(time)));
            _cmd.SetGlobalVector(CosTimeId, new Vector4(Mathf.Cos(time / 8f), Mathf.Cos(time / 4f), Mathf.Cos(time / 2f), Mathf.Cos(time)));
            _cmd.SetGlobalVector(DeltaTimeId, new Vector4(deltaTime, 1f / deltaTime, smoothDeltaTime, 1f / smoothDeltaTime));
        }
    }
}
