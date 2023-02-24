/* DEPRECATED */

using System.Collections;
using System.Collections.Generic;
using System.IO;

#if UNITY_STANDALONE_WIN || (UNITY_EDITOR_WIN && !UNITY_WEBGL && !UNITY_ANDROID)
using Winforms = System.Windows.Forms;
using Sysdraw = System.Drawing;

using System.Runtime.InteropServices; //Dllimport
using System.Text; //StringBuilder

using HWND = System.IntPtr;
#endif

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DesktopRenderBehavior : MonoBehaviour, OnlineTex {

	public GameObject MainButton;
	public GameObject MainPanel;

	public TMP_InputField ProcessesInputField;

	public Slider ProcessNumSlider;

	public Slider UpSlider;
	public Slider LeftSlider;
	public Slider RightSlider;
	public Slider DownSlider;

	private Texture2D _placeholderTexture {get {return StaticGOs.PlaceholderTexture;}}
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
		WindowManager.SetCurrentWindow(MainPanel);

		if (MainPanel.activeSelf)
			SetPanel();
	}

	public Texture2D GetTex() {
		Texture2D texture = GetTexture();

		if (texture == null)
			texture = _placeholderTexture;

		return texture;
	}

#if UNITY_STANDALONE_WIN || (UNITY_EDITOR_WIN && !UNITY_WEBGL && !UNITY_ANDROID)

	private List<KeyValuePair<HWND, string>> _windows;
	private HWND _hwnd;

	private class User32 {
		[StructLayout(LayoutKind.Sequential)]
		public struct Rect {
			public int left;
			public int top;
			public int right;
			public int bottom;
		}

		[DllImport("USER32.DLL")]
  		public static extern HWND GetWindowRect(HWND hWnd, ref User32.Rect rect);

		[DllImport("USER32.DLL")]
		public static extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam);

		[DllImport("USER32.DLL")]
		public static extern int GetWindowText(HWND hWnd, StringBuilder lpString, int nMaxCount);

		[DllImport("USER32.DLL")]
		public static extern int GetWindowTextLength(HWND hWnd);

		[DllImport("USER32.DLL")]
		public static extern bool IsWindowVisible(HWND hWnd);

		[DllImport("USER32.DLL")]
		public static extern HWND GetShellWindow();
	}

	public void StartRendering() {
		int processNum = (int) ProcessNumSlider.value;
		_hwnd = _windows[processNum].Key;
	}

	private Texture2D GetTexture() {
		// https://stackoverflow.com/questions/891345/get-a-screenshot-of-a-specific-application

		if (_hwnd == null) return _placeholderTexture;

		User32.Rect rect = new User32.Rect();
		User32.GetWindowRect(_hwnd, ref rect);

		int width = rect.right - rect.left;
  		int height = rect.bottom - rect.top;

		float mup = UpSlider.value;
		float mleft = LeftSlider.value;
		float mright = RightSlider.value;
		float mdown = DownSlider.value;

		rect.top += (int) ((height / 2f) * (1 - (mup / 100)));
		rect.left += (int) ((width / 2f) * (1 - (mleft / 100)));
		rect.right -= (int) ((width / 2f) * (1 - (mright / 100)));
		rect.bottom -= (int) ((height / 2f) * (1 - (mdown / 100)));

		width = rect.right - rect.left;
  		height = rect.bottom - rect.top;

		if (width * height == 0)
			return _placeholderTexture;
		
		using (Sysdraw.Bitmap bitmap = new Sysdraw.Bitmap(width, height)) {
			using (Sysdraw.Graphics graphic = Sysdraw.Graphics.FromImage(bitmap)) {
				//graphic.CopyFromScreen(Sysdraw.Point.Empty, Sysdraw.Point.Empty, bounds.Size);
				graphic.CopyFromScreen(rect.left, rect.top, 0, 0, new Sysdraw.Size(width, height), Sysdraw.CopyPixelOperation.SourceCopy);
			}

			using (MemoryStream ms = new MemoryStream()) {
				bitmap.Save(ms, bitmap.RawFormat);
				_texture.LoadImage(ms.ToArray());
			}
		}

		return _texture;
	}

	private delegate bool EnumWindowsProc(HWND hWnd, int lParam);

	private void SetPanel() {
		// https://stackoverflow.com/questions/7268302/get-the-titles-of-all-open-windows

		HWND shellWindow = User32.GetShellWindow();
		_windows = new List<KeyValuePair<HWND, string>>(); //make a list so that it can be enumerated

		User32.EnumWindows(delegate(HWND hWnd, int lParam) {
			if (hWnd == shellWindow) return true; //`true` continues
			if (!User32.IsWindowVisible(hWnd)) return true;

			int length = User32.GetWindowTextLength(hWnd);
			if (length == 0) return true;

			StringBuilder builder = new StringBuilder(length);
			User32.GetWindowText(hWnd, builder, length + 1);

			_windows.Add(new KeyValuePair<HWND, string>(hWnd, builder.ToString()));
			return true;

		}, 0);

		ProcessesInputField.text = "";
		for (int i = 0; i < _windows.Count; i++) {
			var window = _windows[i];

			HWND handle = window.Key;
			string title = window.Value;

			ProcessesInputField.text += ($"#{i}: {handle}: {title}\n");
		}

		ProcessNumSlider.minValue = 0;
		ProcessNumSlider.maxValue = _windows.Count - 1;
	}

#else

	public void StartRendering() {
		Debug.LogError("Not implemented.");
	}

	private Texture2D GetTexture() {
		Debug.LogError("Not implemented.");
		return _placeholderTexture;
	}

	private void SetPanel() {
		Debug.LogError("Not implemented.");
	}

#endif

	public void Dispose() {}
}