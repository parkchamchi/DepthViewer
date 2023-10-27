// Developed With Love by Ryan Boyer http://ryanjboyer.com <3

using UnityEngine.Rendering.Universal;

namespace Anaglyph3D {
    public class AnaglyphFeature : ScriptableRendererFeature {
        public Settings settings = new Settings();

        private AnaglyphPass pass;

        public override void Create() {
            pass = new AnaglyphPass(settings, "Anaglyph");
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            if (pass.Material == null || settings.layerMask == 0) {
                return;
            }

            renderer.EnqueuePass(pass);
        }
    }
}