using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using TMPro;

public class MainBehavior : MonoBehaviour {
	public TMP_InputField FilepathInputField;
	public TMP_Text FilepathResultText;

	public TMP_Text StatusText;

	public GameObject UI;

	public Slider DepthMultSlider;
	public Slider CameraLocSlider;

	public enum FileTypes {
		NotExists, 
		Dir,
		Img, Vid,
		Depth,
		Unsupported
	};

	public static readonly string[] SupportedImgExts = {
		".jpg", ".png", //tested
	};
	public static readonly string[] SupportedVidExts = {
		".mp4", //tested
		".asf", ".dv", ".m4v", ".mov", ".mpg", ".mpeg", ".ogv", ".vp8", ".webm", ".wmv"
	};
	public static readonly string[] SupportedDepthExts = {DepthFileUtils.DepthExt};

	private FileTypes _currentFileType;

	private MeshBehavior _meshBehavior;
	private DepthModelBehavior _depthModelBehavior;
	private CameraBehavior _cameraBehavior;
	private DepthONNX _donnx;

	private int _x, _y;
	int _orig_width, _orig_height;
	//float[] _depths;

	private string _orig_filepath;
	private string _hashval;

	private VideoPlayer _vp;
	private float[][] _depths_frames; //for video
	private long _startFrame;
	private long _currentFrame;
	private string _depthfilepath; //path to the depth file read for a video, null if not exists.
	private Dictionary<string, string> _metadata;

	void Start() {
		_meshBehavior = GameObject.Find("DepthPlane").GetComponent<MeshBehavior>();
		_depthModelBehavior = GameObject.Find("DepthModel").GetComponent<DepthModelBehavior>();
		GetBuiltInModel();
		_cameraBehavior = GameObject.Find("XRRig").GetComponent<CameraBehavior>();

		_vp = GameObject.Find("Video Player").GetComponent<VideoPlayer>();
		_vp.frameReady += OnFrameReady;
	}

	void Update() {
		if (_currentFileType == FileTypes.Vid && _vp != null)
			UpdateVideoDepth();

		if (Input.GetMouseButtonDown(1))
			HideUI();
	}

	private void OnFrameReady(VideoPlayer vp, long frame) {
		/* 
		This handler only gets invoked once for the first frame.
		parameter `frame` is the index of the first frame,
		which is usually 0, but some video files start with 1 (or more).
		for _depths_frame, subtract this so that the first frame is always 0.
		*/
		_startFrame = frame;

		/* Set original width/height & framecount for first time */
		_orig_width = (int) vp.width;
		_orig_height = (int) vp.height;

		if (_depths_frames == null) // i.e. _depthFilePath == null
			_depths_frames = new float[_vp.frameCount][];
		
		//Disable
		vp.sendFrameReadyEvents = false;
	}

	private void UpdateVideoDepth() {
		if (_currentFileType != FileTypes.Vid) return;
	
		long frame = _vp.frame;
		if (frame == _currentFrame) 
			return;
		_currentFrame = frame;

		if (frame < 0)
			return;

		Texture texture = _vp.texture;
		if (texture == null) return;


		//Check if the frame was already processed
		if (_depths_frames[frame-_startFrame] == null) {
			//Run the model
			if (_donnx == null) return;

			_depths_frames[frame-_startFrame] = (float[]) _donnx.Run(texture, out _x, out _y).Clone(); //DepthONNX.Run() returns its private member... Took eternity to debug

			StatusText.text = "processed";
		}
		else {
			StatusText.text = "loaded";
		}

		_meshBehavior.SetScene(_depths_frames[frame-_startFrame], _x, _y, (float) _orig_width/_orig_height, texture);
	}

	public void Quit() {
		SaveDepth(); //save the current one

		if (_vp != null)
			Destroy(_vp);

		if (_donnx != null)
			_donnx.Dispose();
		_donnx = null;

		if (_meshBehavior != null)
			Destroy(_meshBehavior);

		Debug.Log("Disposed.");

		Application.Quit();
	}

	public void CheckFileExists() {
		string filepath = FilepathInputField.text;
		FileTypes ftype = GetFileType(filepath);
		string output = "DEBUG: Defulat value, should not be seen.";

		//Check if the file exists
		switch (ftype) {
		case FileTypes.NotExists:
			output = "File does not exist.";
			break;
		case FileTypes.Dir:
			output = "Directory.";
			break;
		case FileTypes.Img:
			output = "Image.";
			break;
		case FileTypes.Vid:
			output = "Video.";
			break;
		case FileTypes.Depth:
			output = "Depth file.";
			break;
		case FileTypes.Unsupported:
			output = "Unsupported.";
			break;
		}

		FilepathResultText.text = output;
	}

	public void SelectFile() {
		/*
			Check if the depth file exists & load it if if exists.
			If new image/video was selected and the previous one was a video, save it.
		*/

		string filepath = FilepathInputField.text;
		FileTypes ftype = GetFileType(filepath);

		if (ftype != FileTypes.Img && ftype != FileTypes.Vid) return;

		SaveDepth();

		_currentFileType = ftype;
		_orig_filepath = filepath;
		_hashval = Utils.GetHashval(filepath);

		if (ftype == FileTypes.Img) FromImage(filepath);
		if (ftype == FileTypes.Vid) FromVideo(filepath);
	}

	public static FileTypes GetFileType(string filepath) {
		if (!File.Exists(filepath)) 
			return FileTypes.NotExists;

		if (Directory.Exists(filepath))
			return FileTypes.Dir;

		foreach (string ext in SupportedImgExts)
			if (filepath.EndsWith(ext)) return FileTypes.Img;

		foreach (string ext in SupportedVidExts)
			if (filepath.EndsWith(ext)) return FileTypes.Vid;

		foreach (string ext in SupportedDepthExts)
			if (filepath.EndsWith(ext)) return FileTypes.Depth;

		return FileTypes.Unsupported;
	}

	private void FromImage(string filepath) {

		Texture texture = Utils.LoadImage(filepath);
		_orig_width = texture.width;
		_orig_height = texture.height;

		int modelTypeVal;
		float[] depths;

		//Check if the file was processed
		string _depthFilePath = DepthFileUtils.ProcessedDepthFileExists(_hashval, out modelTypeVal);
		if (_depthFilePath != null) {
			_depths_frames = DepthFileUtils.ReadDepthFile(_depthFilePath, out _x, out _y, out _metadata);
			depths = _depths_frames[0];

			FilepathResultText.text = $"Depth file read! ModelTypeVal: {modelTypeVal}";
		}

		else {
			depths = _donnx.Run(texture, out _x, out _y);

			/* Save */
			_depths_frames = new float[1][];
			_depths_frames[0] = depths;

			FilepathResultText.text = "Processed!";
		}

		//For metadata
		_startFrame = 0;

		_meshBehavior.SetScene(depths, _x, _y, (float) _orig_width/_orig_height, texture);
	}

	private void FromVideo(string filepath) {
		/* _orig_width, _orig_height, & framecount should be set when the frame is recieved!*/
		/* 
			_depths_frames: [
				null,
				float[] depths for frame=1,
				null,
				...
			]
		*/

		int modelTypeVal;

		/* Check if the processed file exists */
		string _depthFilePath = DepthFileUtils.ProcessedDepthFileExists(_hashval, out modelTypeVal);
		if (_depthFilePath != null) {
			_depths_frames = DepthFileUtils.ReadDepthFile(_depthFilePath, out _x, out _y, out _metadata);

			//Set startframe also
			//It is set to negative if it couldn't be determined - in that case we should check it
			_startFrame = long.Parse(_metadata["startframe"]);
			_orig_width = int.Parse(_metadata["original_width"]);
			_orig_height = int.Parse(_metadata["original_height"]);
			_vp.sendFrameReadyEvents = (_startFrame < 0) ? true : false;

			FilepathResultText.text = $"Depth file read! ModelTypeVal: {modelTypeVal}";
		}
		else {
			_depths_frames = null; //set on update
			_startFrame = 0; //should be reset in the handler
			_vp.sendFrameReadyEvents = true; //have vp send events when frame is ready -- so that we can catch the first frame & check if the first frame starts with 0
		}

		/* Load the video -- should be done after hash is computed */
		_vp.url = filepath;
		
		_currentFrame = -1;
	}

	private void SaveDepth() {
		if (_depths_frames == null) return;
		if (_orig_width*_orig_height*_x*_y == 0) return;

		if (_depthfilepath == null) {
			/* Create a new depth file */
			DepthFileUtils.DumpDepthFile(_depths_frames, _startFrame, _hashval, _orig_filepath, _orig_width, _orig_height, _x, _y, _donnx.ModelTypeVal);
		}
		else {
			/* Update */

			//Should not save if the loaded depth's modeltypeval is higher than the program is using
			int modelTypeVal = int.Parse(_metadata["model_type_val"]);
			if (modelTypeVal == _donnx.ModelTypeVal) { //for now just use ==
				DepthFileUtils.UpdateDepthFile(_depthfilepath, _depths_frames, _x, _y);
			}
		}

		//cleanup
		_depths_frames = null;
		_x = _y = _orig_width = _orig_height = 0;
		_startFrame = _currentFrame = 0;

		StatusText.text = "";
	}

	public void GetBuiltInModel() {
		_donnx = _depthModelBehavior.GetBuiltIn();
	}

	public void CallPythonHybrid() {
		int modelVal = (int) DepthFileUtils.ModelTypes.MidasV3DptHybrid;
		string modelTypeForPy = "dpt_hybrid";

		CallPython(modelVal, modelTypeForPy);
	}

	public void CallPythonLarge() {
		int modelVal = (int) DepthFileUtils.ModelTypes.MidasV3DptLarge;
		string modelTypeForPy = "dpt_large";

		CallPython(modelVal, modelTypeForPy);
	}

	private void CallPython(int modelTypeVal, string modelTypeStringForPython) {
		if (_currentFileType != FileTypes.Img && _currentFileType != FileTypes.Vid)
			return;

		const string pythonPath = @"python"; //todo: change
		const string pythonTarget = @"../depthpy/depth.py";

		string isVideo = (_currentFileType == FileTypes.Vid) ? " -v " : " ";

		string depthFilename = DepthFileUtils.GetDepthFileName(Path.GetFileName(_orig_filepath), modelTypeVal, _hashval);

		System.Diagnostics.Process.Start(pythonPath, $" \"{pythonTarget}\" \"{_orig_filepath}\" \"{depthFilename}\" {isVideo} -t {modelTypeStringForPython} --zip_in_memory");
	}

	public void HideUI() {
		UI.SetActive(!UI.activeSelf);
	}

	public void SetDepthMult() {
		_meshBehavior.SetDepthMult(DepthMultSlider.value);
	}

	public void SetCameraLoc() {
		_cameraBehavior.SetZ(CameraLocSlider.value);
	}
} 
