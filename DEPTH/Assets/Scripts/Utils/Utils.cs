using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

using UnityEngine;

using IngameDebugConsole;

public static class Utils {
	public static string OptionsPath {get {return $"{DepthFileUtils.SaveDir}/options.txt";}}
	public static string PythonPath {get; set;} = "python"; //virtually not used anymore

	[ConsoleMethod("set_python_path", "Set the path for python, which were used for the Call Python buttons (replaced by `send_msg CallPythonHybrid` and `...Large`)")]
	public static void SetPythonPath(string path) =>
		PythonPath = path;

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
}