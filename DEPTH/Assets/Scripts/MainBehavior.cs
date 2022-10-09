using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEngine.Video;
using TMPro;

public class MainBehavior : MonoBehaviour {
	public TMP_InputField FilepathInputField;
	public TMP_Text FilepathResultText;

	public enum FileTypes {
		NotExists, 
		Dir,
		Img, Vid,
		Depth,
		Unsupported
	};

	public static readonly string[] SupportedImgExts = {".jpg", ".png"};
	public static readonly string[] SupportedVidExts = {".mp4"};
	public static readonly string[] SupportedDepthExts = {DepthFileUtils.DepthExt};

	private FileTypes _currentFileType;

	private MeshBehavior _meshBehavior;
	private DepthONNX _donnx;

	private int _x, _y;
	int _orig_width, _orig_height;
	float[] _depths;

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
		GetDepthONNX();

		_vp = GameObject.Find("Video Player").GetComponent<VideoPlayer>();
		_vp.frameReady += OnFrameReady;
	}

	void Update() {
		if (_currentFileType == FileTypes.Vid && _vp != null)
			UpdateVideoDepth();
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
			_depths = _donnx.Run(texture, out _x, out _y);
			_depths_frames[frame-_startFrame] = _depths;
		}

		_meshBehavior.SetScene(_depths, _x, _y, (float) _orig_width/_orig_height, texture);
	}

	private void GetDepthONNX() {
		if (_donnx == null)
			_donnx = GameObject.Find("DepthONNX").GetComponent<DepthONNXBehavior>().GetDepthONNX();
	}

	public void Quit() {
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

		//Check if the file was processed
		List<string> filelist = DepthFileUtils.ProcessedDepthFileExists(_hashval);
		if (filelist.Count > 0) {
			_depths_frames = DepthFileUtils.ReadDepthFile(filelist[0], out _x, out _y, out _metadata);
			_depths = _depths_frames[0];

			FilepathResultText.text = "Depth file read!";
		}

		else {
			_depths = _donnx.Run(texture, out _x, out _y);

			/* Save */
			_depths_frames = new float[1][];
			_depths_frames[0] = _depths;

			FilepathResultText.text = "Processed!";
		}

		//For metadata
		_startFrame = 0;

		_meshBehavior.SetScene(_depths, _x, _y, (float) _orig_width/_orig_height, texture);
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

		/* Check if the processed file exists */
		List<string> filelist = DepthFileUtils.ProcessedDepthFileExists(_hashval);
		if (filelist.Count > 0) {
			_depthfilepath = filelist[0];
			_depths_frames = DepthFileUtils.ReadDepthFile(_depthfilepath, out _x, out _y, out _metadata);

			//set startframe also
			_startFrame = long.Parse(_metadata["startframe"]);

			_vp.sendFrameReadyEvents = false;
			FilepathResultText.text = "Depth file read!";
		}
		else {
			_depthfilepath = null;
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

		if (_depthfilepath == null) {
			/* Create a new depth file */
			DepthFileUtils.DumpDepthFile(_depths_frames, _startFrame, _hashval, _orig_filepath, _orig_width, _orig_height, _x, _y, _donnx.ModelType, _donnx.Weight);
		}
		else {
			/* Update */
			DepthFileUtils.UpdateDepthFile(_depthfilepath, _depths_frames, _x, _y);
		}
	}
} 
