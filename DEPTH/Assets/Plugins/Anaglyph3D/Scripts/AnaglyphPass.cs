// Developed With Love by Ryan Boyer http://ryanjboyer.com <3

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Anaglyph3D {
    public class AnaglyphPass : ScriptableRenderPass {
        private static readonly int[] RenderTargetIDs = new int[2] {
            Shader.PropertyToID("_LeftTex"),
            Shader.PropertyToID("_RightTex")
        };

#if !UNITY_IOS && !UNITY_TVOS
        private static readonly int IntermediateRenderTargetID = Shader.PropertyToID("_IntermediateRenderTarget");
        private RenderTargetIdentifier intermediate;
#endif

        private RenderTargetIdentifier source;
        private RenderTargetIdentifier destination;

        private List<ShaderTagId> shaderTagIDs = new List<ShaderTagId>();

        private RenderTargetIdentifier[] renderTargetIdentifiers = null;

        private FilteringSettings filteringSettings;
        private RenderStateBlock renderStateBlock;

        private Settings settings;
        private Matrix4x4[] offsetMatrices = null;

        internal Material Material => settings.Material;

        private LocalKeyword opacityModeAdditiveKeyword;
        private LocalKeyword opacityModeChannelKeyword;
        private LocalKeyword singleChannelKeyword;
        private LocalKeyword overlayEffectKeyword;

        public AnaglyphPass(Settings settings, string tag) {
            profilingSampler = new ProfilingSampler(tag);
            filteringSettings = new FilteringSettings(RenderQueueRange.all, settings.layerMask);

            shaderTagIDs.Add(new ShaderTagId("SRPDefaultUnlit"));
            shaderTagIDs.Add(new ShaderTagId("UniversalForward"));
            shaderTagIDs.Add(new ShaderTagId("UniversalForwardOnly"));
            shaderTagIDs.Add(new ShaderTagId("LightweightForward"));

            renderStateBlock = new RenderStateBlock(RenderStateMask.Raster);

            this.renderPassEvent = settings.renderPassEvent;
            this.settings = settings;

            offsetMatrices = new Matrix4x4[2];
            renderTargetIdentifiers = new RenderTargetIdentifier[2];

            opacityModeAdditiveKeyword = new LocalKeyword(Material.shader, "_OPACITY_MODE_ADDITIVE");
            opacityModeChannelKeyword = new LocalKeyword(Material.shader, "_OPACITY_MODE_CHANNEL");
            singleChannelKeyword = new LocalKeyword(Material.shader, "_SINGLE_CHANNEL");
            overlayEffectKeyword = new LocalKeyword(Material.shader, "_OVERLAY_EFFECT");
        }

        private void CreateOffsetMatrix(float spacing, float lookTarget, int side, ref Matrix4x4 matrix) {
            float xOffset = spacing * side * 0.5f;
            Vector3 offset = Vector3.right * xOffset;
            if (lookTarget != 0) {
                Quaternion lookRotation = Quaternion.LookRotation(new Vector3(xOffset, 0, lookTarget).normalized, Vector3.up);
                matrix = Matrix4x4.TRS(offset, lookRotation, Vector3.one);
            } else {
                matrix = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one);
            }
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData) {
            this.source = renderingData.cameraData.renderer.cameraColorTarget;
            this.destination = renderingData.cameraData.renderer.cameraColorTarget;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) {
            base.Configure(cmd, cameraTextureDescriptor);

            Material.SetKeyword(opacityModeAdditiveKeyword, settings.opacityMode == Settings.OpacityMode.Additive);
            Material.SetKeyword(opacityModeChannelKeyword, settings.opacityMode == Settings.OpacityMode.Channel);
            Material.SetKeyword(singleChannelKeyword, settings.TextureCount == 1);
            Material.SetKeyword(overlayEffectKeyword, settings.overlayEffect);

            CreateOffsetMatrix(settings.spacing, settings.lookTarget, -1, ref offsetMatrices[0]);
            CreateOffsetMatrix(settings.spacing, settings.lookTarget, 1, ref offsetMatrices[1]);

            RenderTextureDescriptor descriptor = cameraTextureDescriptor;
            descriptor.colorFormat = RenderTextureFormat.ARGB32; // comment out this line to enable transparent recordings
            descriptor.useDynamicScale = true;
            descriptor.depthBufferBits = 16;

            int textureCount = settings.TextureCount;
            for (int i = 0; i < settings.TextureCount; i++) {
                cmd.GetTemporaryRT(RenderTargetIDs[i], descriptor);
                renderTargetIdentifiers[i] = new RenderTargetIdentifier(RenderTargetIDs[i]);
                ConfigureTarget(renderTargetIdentifiers[i]);
            }

#if !UNITY_IOS && !UNITY_TVOS
            cmd.GetTemporaryRT(IntermediateRenderTargetID, descriptor);
            this.intermediate = new RenderTargetIdentifier(IntermediateRenderTargetID);
#endif

            //ConfigureClear(ClearFlag.Color | ClearFlag.Depth, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
            SortingCriteria sortingCriteria = SortingCriteria.RenderQueue;
            DrawingSettings drawingSettings = CreateDrawingSettings(shaderTagIDs, ref renderingData, sortingCriteria);

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, profilingSampler)) {
                ScriptableRenderer renderer = renderingData.cameraData.renderer;
                Camera camera = renderingData.cameraData.camera;

                if (settings.TextureCount == 1) { // render only left channel
                    cmd.SetViewMatrix(camera.worldToCameraMatrix);

                    cmd.ClearRenderTarget(true, true, Color.clear);

                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    cmd.SetRenderTarget(renderTargetIdentifiers[0], RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
                    context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);
                } else { // render both channels
                    for (int i = 0; i < 2; i++) {
                        Matrix4x4 viewMatrix = offsetMatrices[i] * camera.worldToCameraMatrix;
                        cmd.SetViewMatrix(viewMatrix);

                        cmd.ClearRenderTarget(true, true, Color.clear);

                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();

                        cmd.SetRenderTarget(renderTargetIdentifiers[i], RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
                        context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings, ref renderStateBlock);
                    }
                }

#if !UNITY_IOS && !UNITY_TVOS
                cmd.Blit(source, intermediate, Material);
                cmd.Blit(intermediate, destination);
#else
                cmd.Blit(source, destination, Material);
#endif
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd) {
            if (cmd == null) {
                throw new System.ArgumentNullException("cmd");
            }

            for (int i = 0; i < settings.TextureCount; i++) {
                cmd.ReleaseTemporaryRT(RenderTargetIDs[i]);
            }
#if !UNITY_IOS && !UNITY_TVOS
            cmd.ReleaseTemporaryRT(IntermediateRenderTargetID);
#endif
        }
    }
}
