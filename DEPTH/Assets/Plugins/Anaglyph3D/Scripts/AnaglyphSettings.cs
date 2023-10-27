// Developed With Love by Ryan Boyer http://ryanjboyer.com <3

using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Anaglyph3D {
    [Serializable]
    public class Settings {
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        [Tooltip("Which layers to include when rendering the effect.")] public LayerMask layerMask = -1;

        [Space, Tooltip("The spacing between the red and cyan channels.  This value may need to be larger for orthographic cameras.")] public float spacing = 0.2f;
        [Tooltip("The focal point, represented as units in front of the camera.")] public float lookTarget = 10f;

        [Tooltip("How should anaglyph layers be rendered on top of normal layers?")] public OpacityMode opacityMode = OpacityMode.None;
        [Tooltip("Overlay the layers with the effect on top of other layers.  This is useful for when only some layers should be rendered with the effect, but is also more computationally expensive.")] public bool overlayEffect = true;

        [Space, Tooltip("The anaglpyh shader, located at the root directory of the package.")] public Shader shader = null;

        internal int TextureCount => spacing == 0 ? 1 : 2;

        public enum OpacityMode : int {
            None = 0,
            Additive = 1,
            Channel = 2
        }

        private Material _material = default;
        public Material Material {
            get {
                if (_material == null && shader != null) {
                    _material = new Material(shader);
                    _material.hideFlags = HideFlags.HideAndDontSave;
                }
                return _material;
            }
        }

		///////////////////////////////////////////
		// START anaglyph_modification
		///////////////////////////////////////////

		public static Settings GlobalSettings = null; //Should the first char capitalized? I can't quite remember the C# convention...
		public Settings() {
			GlobalSettings = this; //Export itself
		}

		///////////////////////////////////////////
		// END anaglyph_modification
		///////////////////////////////////////////
    }
}