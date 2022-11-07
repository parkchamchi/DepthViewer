using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
using Winforms = System.Windows.Forms;
using Sysdraw = System.Drawing;

//using Sysdiag = System.Diagnostics; //Process
using System.Runtime.InteropServices; //Dllimport
using System.Text;

using HWND = System.IntPtr;
#endif

using UnityEngine;
using TMPro;

public class DesktopRenderBehavior : MonoBehaviour {

	public GameObject MainButton;
	public GameObject MainPanel;

	public TMP_InputField ProcessesInputField;
	public TMP_InputField ProcessNumInputField;

	public Texture2D PlaceholderTexture;
	private Texture2D _texture;

	private bool _supported;
	public bool Supported {get {return _supported;}}

	private List<KeyValuePair<HWND, string>> _windows;
	private HWND _hwnd;

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

		if (MainPanel.activeSelf)
			SetPanel();
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

	private class User32 {
		[StructLayout(LayoutKind.Sequential)]
		public struct Rect {
			public int left;
			public int top;
			public int right;
			public int bottom;
		}

		[DllImport("user32.dll")]
  		public static extern IntPtr GetWindowRect(IntPtr hWnd, ref User32.Rect rect);

		[DllImport("USER32.DLL")]
		public static extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam);

		[DllImport("USER32.DLL")]
		public static extern int GetWindowText(HWND hWnd, StringBuilder lpString, int nMaxCount);

		[DllImport("USER32.DLL")]
		public static extern int GetWindowTextLength(HWND hWnd);

		[DllImport("USER32.DLL")]
		public static extern bool IsWindowVisible(HWND hWnd);

		[DllImport("USER32.DLL")]
		public static extern IntPtr GetShellWindow();
	}

	public void StartRendering() {
		string inputString = ProcessNumInputField.text;
		if (inputString == null || inputString == "") return;

		int processnum = int.Parse(inputString);

		if (processnum >= 0 && processnum < _windows.Count)
			_hwnd = _windows[processnum].Key;
	}

	private Texture2D GetTexture() {
		// https://stackoverflow.com/questions/891345/get-a-screenshot-of-a-specific-application

		if (_hwnd == null) return PlaceholderTexture;

		User32.Rect rect = new User32.Rect();
		User32.GetWindowRect(_hwnd, ref rect);

		int width = rect.right - rect.left;
  		int height = rect.bottom - rect.top;
		
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
		//Returns a dictionary that contains the handle and title of all the open windows.
		//A dictionary that contains the handle and title of all the open windows.

		HWND shellWindow = User32.GetShellWindow();
		//Dictionary<HWND, string> windows = new Dictionary<HWND, string>();
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
		//foreach(KeyValuePair<IntPtr, string> window in windows) {
		for (int i = 0; i < _windows.Count; i++) {
			var window = _windows[i];

			IntPtr handle = window.Key;
			string title = window.Value;

			ProcessesInputField.text += ($"#{i}: {handle}: {title}\n");

			if (title.EndsWith("Firefox"))
				_hwnd = handle;
		}
	}

#else

	public void StartRendering() {
		Debug.LogError("Not implemented.");
	}

	private Texture2D GetTexture() {
		Debug.LogError("Not implemented.");
		return PlaceholderTexture;
	}

	private void SetPanel() {
		Debug.LogError("Not implemented.");
	}

#endif

}