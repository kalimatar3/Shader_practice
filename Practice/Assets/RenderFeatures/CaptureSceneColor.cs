using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class CaptureSceneColor : ScriptableRendererFeature
{
    class CapturePass : ScriptableRenderPass
    {
        public string textureName;
        public int blurPasses;
        public float downsample;

        private class PassData
        {
            public TextureHandle source;
            public TextureHandle destination;
            public TextureHandle tempBlur;
            public int blurPasses;
            public string textureName;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Compatibility mode - not used in Render Graph
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            if (cameraData.isPreviewCamera) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            
            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.depthBufferBits = 0;
            desc.width = Mathf.Max(1, Mathf.RoundToInt(desc.width / downsample));
            desc.height = Mathf.Max(1, Mathf.RoundToInt(desc.height / downsample));

            TextureHandle destination = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, textureName, true);
            TextureHandle tempBlur = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_TempBlur", true);
            TextureHandle cameraColor = resourceData.activeColorTexture;

            if (!cameraColor.IsValid()) return;

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Capture Scene Color Pass", out var passData))
            {
                passData.source = cameraColor;
                passData.destination = destination;
                passData.tempBlur = tempBlur;
                passData.blurPasses = blurPasses;
                passData.textureName = textureName;

                builder.UseTexture(passData.source);
                builder.SetRenderAttachment(passData.destination, 0);
                
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawProcedural(Matrix4x4.identity, new Material(Shader.Find("Blit")), 0, MeshTopology.Triangles, 3, 1);

                    for (int i = 0; i < data.blurPasses; i++)
                    {
                        context.cmd.DrawProcedural(Matrix4x4.identity, new Material(Shader.Find("Blit")), 0, MeshTopology.Triangles, 3, 1);
                    }

                    context.cmd.SetGlobalTexture(data.textureName, data.destination);
                });
            }
        }
    }

    [System.Serializable]
    public class Settings
    {
        public string textureName = "_CustomSceneColor";
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        [Range(0, 4)] public int blurPasses = 1;
        [Range(1, 8)] public float downsample = 2.0f;
    }

    public Settings settings = new Settings();
    CapturePass m_ScriptablePass;

    public override void Create()
    {
        m_ScriptablePass = new CapturePass();
        m_ScriptablePass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        m_ScriptablePass.textureName = settings.textureName;
        m_ScriptablePass.blurPasses = settings.blurPasses;
        m_ScriptablePass.downsample = settings.downsample;
        renderer.EnqueuePass(m_ScriptablePass);
    }
}