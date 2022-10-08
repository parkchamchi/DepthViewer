using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using TMPro;

public class MainBehavior : MonoBehaviour {
	public TMP_InputField FilepathInputField;
	public TMP_Text FilepathResultText;

	public static readonly string[] SupportedExts = {".jpg", ".png"};

	private MeshBehavior _meshBehavior;
	private DepthONNX _donnx;

	private int _x, _y;
	int _orig_width, _orig_height;
	float[] _depths;

	void Start() {
		_meshBehavior = GameObject.Find("DepthPlane").GetComponent<MeshBehavior>();
		GetDepthONNX();
	}

	private void GetDepthONNX() {
		if (_donnx == null)
			_donnx = GameObject.Find("DepthONNX").GetComponent<DepthONNXBehavior>().GetDepthONNX();
	}

	public void Quit() {
		if (_donnx != null)
			_donnx.Dispose();
		_donnx = null;

		Debug.Log("Disposed.");

		Application.Quit();
	}

	public void CheckFileExists() {
		string filepath = FilepathInputField.text;

		//Check if the file exists
		if (File.Exists(filepath))
			FilepathResultText.text = "File exists.";
		else if (Directory.Exists(filepath))
			FilepathResultText.text = "Directory.";
		else
			FilepathResultText.text = "File does not exist!";
	}

	public void SelectFile() {
		/*
			Check if the depth file exists & load it if if exists
		*/

		string filepath = FilepathInputField.text;
		if (!File.Exists(filepath) || !IsImage(filepath)) return;

		FromImage(filepath);
	}

	public static bool IsImage(string filepath) {
		/*Returns true if it is supported image file.*/

		if (!File.Exists(filepath)) return false;
		
		foreach (string ext in SupportedExts)
			if (filepath.EndsWith(ext)) return true;
		
		return false;
	}

	public void FromImage(string filepath) {

		/* Check if the file was processed earlier */
		string hashval = Utils.GetHashval(filepath);
		Texture2D texture = Utils.LoadImage(filepath);
		_orig_width = texture.width;
		_orig_height = texture.height;


		//Check if the file was processed
		List<string> filelist = DepthFileUtils.ProcessedDepthFileExists(hashval);
		if (filelist.Count > 0) {
			_depths = DepthFileUtils.ReadDepthFile(filelist[0], out _x, out _y)[0];

			FilepathResultText.text = "Depth file read!";
		}

		else {
			
			_depths = _donnx.Run(texture, out _x, out _y);

			//save
			float[][] depths_frames = new float[1][];
			depths_frames[0] = _depths;
			DepthFileUtils.DumpDepthFile(depths_frames, filepath, _orig_width, _orig_height, _x, _y, _donnx.ModelType, _donnx.Weight);

			FilepathResultText.text = "Processed!";
		}

		_meshBehavior.SetScene(_depths, _x, _y, (float) _orig_width/_orig_height, texture);
	}
}
