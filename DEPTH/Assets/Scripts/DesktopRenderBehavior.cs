using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using Winforms = System.Windows.Forms;
using Sysdraw = System.Drawing;
#endif

using UnityEngine;

public class DesktopRenderBehavior : MonoBehaviour {

	public GameObject MainButton;
	public GameObject MainPanel;

	public Texture2D PlaceholderTexture;
	private Texture2D _texture;

	private bool _supported;
	public bool Supported {get {return _supported;}}

	void Start() {
		/* Only supports Windows */
	#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
		_supported = true;
	#else
		_supported = false;
	#endif

		MainButton.SetActive(_supported);
		MainPanel.SetActive(false);

		_texture = new Texture2D(1, 1);
	}

	public void TogglePanel() {
		MainPanel.SetActive(!MainPanel.activeSelf);
	}

	public Texture2D Get(out int width, out int height) {
		Texture2D texture = GetTexture();

		width = texture.width;
		height = texture.height;

		if (width * height == 0) {
			Debug.LogError($"Invalid texture size: {width}x{height}");

			width = PlaceholderTexture.width;
			height = PlaceholderTexture.height;
			return PlaceholderTexture;
		}

		return texture;
	}

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN

	private Texture2D GetTexture() {
		Winforms.Screen screen = Winforms.Screen.PrimaryScreen;
		Sysdraw.Rectangle bounds = screen.Bounds; //4fps
		//bounds = new Sysdraw.Rectangle(100, 100, 100, 100); //60fps
		
		using (Sysdraw.Bitmap bitmap = new Sysdraw.Bitmap(bounds.Width, bounds.Height)) {
			using (Sysdraw.Graphics graphic = Sysdraw.Graphics.FromImage(bitmap)) {
				graphic.CopyFromScreen(Sysdraw.Point.Empty, Sysdraw.Point.Empty, bounds.Size);
			}

			using (MemoryStream ms = new MemoryStream()) {
				/*bitmap.Save(ms, Sysdraw.Imaging.ImageFormat.Jpeg);

				byte[] buffer = new byte[ms.Length];
				ms.Position = 0;
				ms.Read(buffer, 0, buffer.Length);

				_texture.LoadImage(buffer);
				buffer = null;*/

				bitmap.Save(ms, bitmap.RawFormat);
				_texture.LoadImage(ms.ToArray());
			}
		}

		return _texture;
	}

#else

	private Texture2D GetTexture() {
		Debug.LogError("Not implemented.");
		return PlaceholderTexture;
	}

#endif

	
}
