using IngameDebugConsole;

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using UnityEngine;

public delegate void AddCommandDelegate(string command, string description, string methodName, object instance, params string[] parameterNames);

public static class Utils {
	public static string OptionsPath {get {return $"{DepthFileUtils.SaveDir}/options.txt";}}
	public static string InitCmdsPath {get {return $"{DepthFileUtils.SaveDir}/initcmds.txt";}}

	public static Texture2D LoadImage(string path) {
		if (!File.Exists(path)) {
			Debug.LogError("File " + path + " does not exist.");
			return null;
		}
		byte[] byteArr = File.ReadAllBytes(path);

		return LoadImage(byteArr);
	}

	public static Texture2D LoadImage(byte[] byteArr) {
		Texture2D texture = new Texture2D(0, 0);
		bool isLoaded = texture.LoadImage(byteArr);

		if (isLoaded)
			return texture;
		else
			return null;
	}

	public static string GetHashval(string filepath) {
		if (!File.Exists(filepath))
			Debug.LogFormat("File does not exist! : {0}", filepath);

		byte[] hashbytes;

		using (SHA256 sha256 = SHA256.Create()) {
			using (FileStream fs = File.Open(filepath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
				fs.Position = 0;
				hashbytes = sha256.ComputeHash(fs);
			}
		}

		string hashval = "";
		foreach (byte b in hashbytes)
			hashval += string.Format("{0:x2}", b);

		return hashval;
	}

	public static string GetTimestamp() {
		return DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
	}

	public static void CreateDirectory(string path) {
		if (!Directory.Exists(path))
			Directory.CreateDirectory(path);
	}

	public static CanRunCoroutine GetDummyBehavior() {
		CanRunCoroutine behav = GameObject.Find("DummyObject").GetComponent<DummyBehavior>();
		if (behav == null)
			Debug.LogError("Couldn't get the DummyBehavior!");
			
		return behav;
	}

	//just two boolean vars
	public static void ReadOptionsString(out bool searchCache, out bool saveOutput) {
		searchCache = saveOutput = false;

		if (!File.Exists(OptionsPath)) return;
		string str = File.ReadAllText(OptionsPath);

		foreach (string line in str.Split('\n')) {
			switch (line) {
			case "searchCache":
				searchCache = true;
				break;
			case "saveOutput":
				saveOutput = true;
				break;
			}
		}
	}

	public static void WriteOptionsString(bool searchCache, bool saveOutput) {
		string output = "";
		if (searchCache) output += "searchCache\n";
		if (saveOutput) output += "saveOutput\n";

		File.WriteAllText(OptionsPath, output);
	}

	public static bool IsNaNInf(float val) =>
		(float.IsNaN(val) || float.IsPositiveInfinity(val) || float.IsNegativeInfinity(val));

	public static Texture2D DepthToTex(Depth depth) {
		if (!Depth.IsValid(depth)) {
			Debug.LogError("DepthToPng: got invalid input");
			return null;
		}

		int x = depth.X;
		int y = depth.Y;

		Texture2D tex = new Texture2D(x, y, textureFormat: TextureFormat.RFloat, mipCount: -1, linear: false);
		for (int h = 0; h < y; h++) {
			for (int w = 0; w < x; w++) {
				float val = depth.Value[(y-h-1)*x + w]; //Why is this flipped?
				Color color = new Color(val, val, val, 1); //rgba
				tex.SetPixel(w, h, color);
			}
		}
		tex.Apply();

		return tex;
	}

	public static Texture2D ResizeTexture(Texture2D tex, int w, int h) {
		//Don't use this in loop since RenderTexture is expensive
		//This doesn't destroy the original `tex`.

		RenderTexture rt = new RenderTexture(w, h, 16);
		Graphics.Blit(tex, rt); //tex -> rt

		Texture2D newtex = new Texture2D(rt.width, rt.height, tex.format, -1, false);

		//Move the resized texture
		RenderTexture.active = rt;
		newtex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
		RenderTexture.active = null;

		rt.Release();

		return newtex;
	}

	public static MainBehavior GetMainBehav() =>
		GameObject.Find("MainManager").GetComponent<MainBehavior>();

	public static string DecodeAscii(byte[] bytes) =>
		Encoding.ASCII.GetString(bytes);

	public static byte[] EncodeAscii(string str) =>
		Encoding.ASCII.GetBytes(str);

	public static string DecodeUtf8(byte[] bytes) =>
		Encoding.UTF8.GetString(bytes);

	public static byte[] EncodeUtf8(string str) =>
		Encoding.UTF8.GetBytes(str);

	public static byte[] ConcatByteArray(byte[] arr1, byte[] arr2) {
		byte[] concated = new byte[arr1.Length + arr2.Length];
		arr1.CopyTo(concated, 0);
		arr2.CopyTo(concated, arr1.Length);

		return concated;
	}

	public static string[] GetInitCmds() {
		if (!File.Exists(InitCmdsPath)) {
			File.WriteAllText(InitCmdsPath, "echo Enter_the_commands_here_(may_cause_inconsistencies_on_the_UI)");
			return null;
		}

		string str = File.ReadAllText(InitCmdsPath);

		return str.Split('\n');
	}

	[ConsoleMethod("echo", "Echo")]
	public static void Echo(string str) =>
		Debug.Log($"Echo: {str}");

	[ConsoleMethod("start_process", "Start a process. Delimit with `*` (e.g. `python*ffpymq.py*--optimize)")]
	public static void StartProcess(string str) {
		string delimiter = "*";
		
		string[] split = str.Split(delimiter, 2);
		string path = split[0];
		string args = (split.Length > 1) ? split[1].Replace(delimiter, " ") : null;

		StartProcess(path, args);
	}

	public static void StartProcess(string path, string args) {
		Debug.Log($"Starting the process: `{path}` `{args}`");
		System.Diagnostics.Process.Start(path, args);
	}

	[ConsoleMethod("sleep", "Sleep for msec")]
	public static void Sleep(int msec) {
		Debug.Log($"Sleeping. ({msec} msec)");
		System.Threading.Thread.Sleep(msec);
	}

	[ConsoleMethod("set_camera_bg", "Set the background color for the camera.")]
	public static void SetCameraBg(int r, int g, int b, int a) {
		Camera camera = GameObject.Find("MainCamera").GetComponent<Camera>();

		Color c = new Color(r, g, b, a);
		Debug.Log($"Setting the background color to {c}");
		camera.backgroundColor = c;
	}
}