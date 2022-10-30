using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using TMPro;

public class MainBehavior : MonoBehaviour {

	public Slider DepthMultSlider;

	public TMP_InputField FilepathInputField;
	public TMP_Text FilepathResultText;

	public TMP_Text StatusText;

	public Toggle OutputSaveToggle;
	public TMP_Text OutputSaveText;

	public GameObject UI;

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

	private FileTypes _currentFileType = FileTypes.NotExists;

	private MeshBehavior _meshBehavior;
	private DepthModelBehavior _depthModelBehavior;
	private DepthONNX _donnx;

	private int _x, _y;
	int _orig_width, _orig_height;

	private string _orig_filepath;
	private string _hashval;

	private VideoPlayer _vp;
	private float[][] _depths_frames; //for video
	private long _startFrame;
	private long _currentFrame;
	private string _depthFilePath; //path to the depth file read for a video, null if not exists.
	private Dictionary<string, string> _metadata;

	private bool _canUpdateArchive; //User option, toggled in UI; variable below overrides this
	private bool _shouldUpdateArchive;
	private bool _hasCreatedArchive;
	private List<Task> _processedFrames;

	void Start() {
		_meshBehavior = GameObject.Find("DepthPlane").GetComponent<MeshBehavior>();
		_depthModelBehavior = GameObject.Find("DepthModel").GetComponent<DepthModelBehavior>();
		GetBuiltInModel();

		_vp = GameObject.Find("Video Player").GetComponent<VideoPlayer>();
		_vp.frameReady += OnFrameReady;
		_vp.errorReceived += OnVideoError;

		ToggleOutputSave(); //initializing _canUpdadeArchive
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

		if (_depths_frames == null) { // i.e. _depthFilePath == null
			_depths_frames = new float[_vp.frameCount][];
		}
		
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

		long actualFrame = frame-_startFrame;

		//Check if the frame was already processed
		if (_depths_frames[actualFrame] == null) {
			//If depth file exists, try to read from it
			if (_depthFilePath != null)
				_depths_frames[actualFrame] = DepthFileUtils.ReadFromArchive(actualFrame);

			if (_depths_frames[actualFrame] != null) 
				StatusText.text = "read from archive";

			else {
				//Run the model
				if (_donnx == null) return;
				_depths_frames[actualFrame] = (float[]) _donnx.Run(texture, out _x, out _y).Clone(); //DepthONNX.Run() returns its private member...

				/* For a new media, create the depth file */
				if (_depthFilePath == null && !_hasCreatedArchive && _shouldUpdateArchive) {
					DepthFileUtils.CreateDepthFile(_depths_frames.Length, _startFrame, _hashval, _orig_filepath, _orig_width, _orig_height, _x, _y, _donnx.ModelTypeVal);
					_hasCreatedArchive = true;
				}

				//Save it
				if (_shouldUpdateArchive)
					_processedFrames.Add(Task.Run(() => DepthFileUtils.UpdateDepthFile(_depths_frames[actualFrame], actualFrame, _x, _y)));

				StatusText.text = "processed";
			}
		}
		else {
			StatusText.text = "loaded";
		}

		_meshBehavior.SetScene(_depths_frames[actualFrame], _x, _y, (float) _orig_width/_orig_height, texture);
	}

	public void Quit() {
		SaveDepth(); //save the current one
		
		DepthFileUtils.Dispose();

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
		string output = "DEBUG: Default value, should not be seen.";

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
		_processedFrames = new List<Task>();
		_hasCreatedArchive = false;

		if (_shouldUpdateArchive = _canUpdateArchive) //assign & compare
			OutputSaveText.text = "Will be saved.";
		else
			OutputSaveText.text = "Won't be saved.";

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

		//For metadata
		_startFrame = 0;

		int modelTypeVal;
		float[] depths;

		//Check if the file was processed
		_depthFilePath = DepthFileUtils.ProcessedDepthFileExists(_hashval, out modelTypeVal);
		if (_depthFilePath != null) {
			_depths_frames = DepthFileUtils.ReadDepthFile(_depthFilePath, out _x, out _y, out _metadata, readOnlyMode: true);
			depths = _depths_frames[0] = DepthFileUtils.ReadFromArchive(0);

			FilepathResultText.text = $"Depth file read! ModelTypeVal: {modelTypeVal}";
		}

		else {
			depths = _donnx.Run(texture, out _x, out _y);

			/* Save */
			_depths_frames = new float[1][];
			_depths_frames[0] = depths;

			if (_shouldUpdateArchive) {
				DepthFileUtils.CreateDepthFile(1, _startFrame, _hashval, _orig_filepath, _orig_width, _orig_height, _x, _y, _donnx.ModelTypeVal);

				_processedFrames.Add(Task.Run(() => DepthFileUtils.UpdateDepthFile(depths, 0, _x, _y)));
				_hasCreatedArchive = true; //not needed
			}

			FilepathResultText.text = "Processed!";
		}

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
		_depthFilePath = DepthFileUtils.ProcessedDepthFileExists(_hashval, out modelTypeVal);
		if (_depthFilePath != null) {
			//Should not save if the loaded depth's modeltypeval is higher than the program is using
			if (modelTypeVal != _donnx.ModelTypeVal) {//for now just use !=
				_shouldUpdateArchive = false;
				OutputSaveText.text = "Not saving.";
			}

			_depths_frames = DepthFileUtils.ReadDepthFile(_depthFilePath, out _x, out _y, out _metadata, readOnlyMode: !_shouldUpdateArchive);

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

		/* Wait */
		Task.WaitAll(_processedFrames.ToArray());

		if (_currentFileType == FileTypes.Vid) {
			_vp.Stop();
			_vp.url = null;
		}

		//cleanup -- may overlap w/ SelectFile()
		_depths_frames = null;
		_x = _y = _orig_width = _orig_height = 0;
		_startFrame = _currentFrame = 0;
		_processedFrames = null;

		DepthFileUtils.Dispose();

		StatusText.text = "";
	}

	public void GetBuiltInModel() {
		_donnx = _depthModelBehavior.GetBuiltIn();
	}

	public void CallPythonHybrid() {
		CallPython(DepthFileUtils.ModelTypes.MidasV3DptHybrid);
	}

	public void CallPythonLarge() {
		CallPython(DepthFileUtils.ModelTypes.MidasV3DptLarge);
	}

	private void CallPython(DepthFileUtils.ModelTypes modelType) {
		if (_currentFileType != FileTypes.Img && _currentFileType != FileTypes.Vid)
			return;

		const string pythonPath = @"python"; //todo: change
		const string pythonTarget = @"../depthpy/depth.py";

		string isVideo = (_currentFileType == FileTypes.Vid) ? " -v " : " ";

		int modelTypeVal = (int) modelType;
		string modelTypeString = modelType.ToString();

		string depthFilename = DepthFileUtils.GetDepthFileName(Path.GetFileName(_orig_filepath), modelTypeVal, _hashval);

		System.Diagnostics.Process.Start(pythonPath, $" \"{pythonTarget}\" \"{_orig_filepath}\" \"{depthFilename}\" {isVideo} -t {modelTypeString} --zip_in_memory");
	}

	public void HideUI() {
		UI.SetActive(!UI.activeSelf);
	}

	public void SetDepthMult() {
		float rat = DepthMultSlider.value;
		/* Depth has to be updated when the image is being shown */
		bool shouldUpdate = (_currentFileType == FileTypes.Img);

		_meshBehavior.SetDepthMult(rat, shouldUpdate);
	}

	public void ToggleFullscreen() {
		if (Screen.fullScreenMode == FullScreenMode.Windowed)
			Screen.fullScreenMode = FullScreenMode.MaximizedWindow;
		else
			Screen.fullScreenMode = FullScreenMode.Windowed;
	}

	public void ToggleOutputSave() {
		_canUpdateArchive = OutputSaveToggle.isOn;
	}

	private void OnVideoError(VideoPlayer vp, string message) {
		FilepathResultText.text = "Failed to load video: " + message;
		_vp.Stop();
		_vp.url = "";
	}
} 
