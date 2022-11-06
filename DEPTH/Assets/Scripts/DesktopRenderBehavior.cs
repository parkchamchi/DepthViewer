using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;

public class DesktopRenderBehavior : MonoBehaviour {

	public GameObject MainButton;
	public GameObject MainPanel;

	public Texture2D PlaceholderTexture;

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
	}

	public void TogglePanel() {
		MainPanel.SetActive(!MainPanel.activeSelf);
	}

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN

	//[DllImport("user32.dll")]
    //private static extern IntPtr GetDesktopWindow();

	public Texture2D Get(out int width, out int height) {
		//IntPtr hwnd = GetDesktopWindow();

		Texture2D texture = PlaceholderTexture;

		width = texture.width;
		height = texture.height;

		return texture;
	}

#endif

	
}
