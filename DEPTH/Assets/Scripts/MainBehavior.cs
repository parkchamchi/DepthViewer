using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using SFB;

using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using TMPro;

public class MainBehavior : MonoBehaviour {

	/* These sliders should be here to check if we need to update depth for images. */
	public Slider DepthMultSlider;
	public Slider AlphaSlider;
	public Slider BetaSlider;

	public TMP_InputField FilepathInputField;
	public TMP_Text FilepathResultText;

	public TMP_Text StatusText;

	public Toggle OutputSaveToggle;
	public TMP_Text OutputSaveText;

	public GameObject UI;
	public GameObject AboutScreen;
	public TMP_Text AboutText;
	public TextAsset AboutTextAsset;

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
	private long _startFrame;
	private long _currentFrame;
	private long _framecount;
	private string _depthFilePath; //path to the depth file read for a video, null if not exists.
	private Dictionary<string, string> _metadata;

	private bool _canUpdateArchive; //User option, toggled in UI; variable below overrides this
	private bool _shouldUpdateArchive;
	private bool _hasCreatedArchive;
	private List<Task> _processedFrames;

	private ExtensionFilter[] _extFilters;

	void Start() {
		_meshBehavior = GameObject.Find("DepthPlane").GetComponent<MeshBehavior>();
		_depthModelBehavior = GameObject.Find("DepthModel").GetComponent<DepthModelBehavior>();
		GetBuiltInModel();

		_vp = GameObject.Find("Video Player").GetComponent<VideoPlayer>();
		_vp.frameReady += OnFrameReady;
		_vp.errorReceived += OnVideoError;
		_vp.loopPointReached += OnLoopPointReached;

		ToggleOutputSave(); //initializing _canUpdadeArchive

		/* Check the first arguement */
		string[] args = System.Environment.GetCommandLineArgs();
		if (args.Length > 1) {
			string arg = args[1];
			FileTypes argFileType = GetFileType(arg);

			if (argFileType == FileTypes.Img || argFileType == FileTypes.Vid) {
				FilepathInputField.text = arg;
				SelectFile();
			}
		}

		/* Set ExtensionFilter for StandalonFileBrowser */
		//remove '.'
		string[] exts = new string[SupportedImgExts.Length + SupportedVidExts.Length];
		int idx = 0;
		for (int i = 0; i < SupportedImgExts.Length; i++)
			exts[idx++] = SupportedImgExts[i].Substring(1, SupportedImgExts[i].Length-1);
		for (int i = 0; i < SupportedVidExts.Length; i++)
			exts[idx++] = SupportedVidExts[i].Substring(1, SupportedVidExts[i].Length-1);

		_extFilters = new [] {
			new ExtensionFilter("Image/Video Files", exts),
		};

		/* Set about screen */
		CloseAboutScreen(); //redundant
		AboutText.text = AboutTextAsset.text;
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
		For _currentFrame, subtract this so that the first frame is always 0.
		*/
		_startFrame = frame;

		/* Set original width/height & framecount for first time */
		_orig_width = (int) vp.width;
		_orig_height = (int) vp.height;
		
		//Disable
		vp.sendFrameReadyEvents = false;
	}

	private void OnLoopPointReached(VideoPlayer vp) {
		SaveDepth(shouldReload: true);

		/* Now read the saved depths */
		if (_depthFilePath == null && _hasCreatedArchive) {
			_depthFilePath = DepthFileUtils.ProcessedDepthFileExists(_hashval, out _);
		}
	}

	private void UpdateVideoDepth() {
		if (_currentFileType != FileTypes.Vid) return;
	
		long frame = _vp.frame;
		if (frame == _currentFrame) 
			return;
		_currentFrame = frame;

		if (frame < 0)
			return;
		long actualFrame = frame-_startFrame;

		Texture texture = _vp.texture;
		if (texture == null) return;

		float[] depths = null;
		
		//If depth file exists, try to read from it
		if (_depthFilePath != null)
			depths = DepthFileUtils.ReadFromArchive(actualFrame);

		if (depths != null) 
			StatusText.text = "read from archive";
		else {
			//Run the model
			if (_donnx == null) return;
			depths = _donnx.Run(texture, out _x, out _y);

			/* For a new media, create the depth file */
			if (_depthFilePath == null && !_hasCreatedArchive && _shouldUpdateArchive) {
				DepthFileUtils.CreateDepthFile(depths.Length, _startFrame, _hashval, _orig_filepath, _orig_width, _orig_height, _x, _y, _donnx.ModelTypeVal);
				_hasCreatedArchive = true;
			}

			//Save it
			if (_shouldUpdateArchive)
				_processedFrames.Add(Task.Run(() => DepthFileUtils.UpdateDepthFile(depths, actualFrame, _x, _y)));

			StatusText.text = "processed";
		}
		
		_meshBehavior.SetScene(depths, _x, _y, (float) _orig_width/_orig_height, texture);
	}

	public void HaltVideo() {
		if (_vp == null) return;
		_vp.Stop();
		_vp.url = null;
	}

	public void Quit() {
		SaveDepth(); //save the current one
		HaltVideo();
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
		HaltVideo();
		DepthFileUtils.Dispose();

		_currentFileType = ftype;
		_orig_filepath = filepath;
		_hashval = Utils.GetHashval(filepath);
		_processedFrames = new List<Task>();
		_hasCreatedArchive = false;
		_startFrame = _currentFrame = _framecount = 0;
		_x = _y = _orig_width = _orig_height = 0;
		
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

		/* Couldn't load */
		if (texture == null) {
			FilepathResultText.text = "Failed to load image: " + filepath;
			return;
		}

		_orig_width = texture.width;
		_orig_height = texture.height;

		//For metadata
		_startFrame = 0;

		int modelTypeVal;
		float[] depths;

		//Check if the file was processed
		_depthFilePath = DepthFileUtils.ProcessedDepthFileExists(_hashval, out modelTypeVal);
		if (_depthFilePath != null) {
			_framecount = DepthFileUtils.ReadDepthFile(_depthFilePath, out _x, out _y, out _metadata, readOnlyMode: true);
			depths = DepthFileUtils.ReadFromArchive(0);

			FilepathResultText.text = $"Depth file read! ModelTypeVal: {modelTypeVal}";
		}

		else {
			depths = _donnx.Run(texture, out _x, out _y);

			/* Save */
			_framecount = 1;

			if (_shouldUpdateArchive) {
				DepthFileUtils.CreateDepthFile(_framecount, _startFrame, _hashval, _orig_filepath, _orig_width, _orig_height, _x, _y, _donnx.ModelTypeVal);

				_processedFrames.Add(Task.Run(() => DepthFileUtils.UpdateDepthFile(depths, 0, _x, _y)));
				_hasCreatedArchive = true; //not needed
			}

			FilepathResultText.text = "Processed!";
		}

		_meshBehavior.SetScene(depths, _x, _y, (float) _orig_width/_orig_height, texture);
	}

	private void FromVideo(string filepath) {
		/* _orig_width, _orig_height, & framecount should be set when the frame is recieved!*/

		int modelTypeVal;

		/* Check if the processed file exists */
		_depthFilePath = DepthFileUtils.ProcessedDepthFileExists(_hashval, out modelTypeVal);
		if (_depthFilePath != null) {
			//Should not save if the loaded depth's modeltypeval is higher than the program is using
			if (modelTypeVal != _donnx.ModelTypeVal) {//for now just use !=
				_shouldUpdateArchive = false;
				OutputSaveText.text = "Not saving.";
			}

			_framecount = DepthFileUtils.ReadDepthFile(_depthFilePath, out _x, out _y, out _metadata, readOnlyMode: !_shouldUpdateArchive);

			//Set startframe also
			//It is set to negative if it couldn't be determined - in that case we should check it
			_startFrame = long.Parse(_metadata["startframe"]);
			_orig_width = int.Parse(_metadata["original_width"]);
			_orig_height = int.Parse(_metadata["original_height"]);
			_vp.sendFrameReadyEvents = (_startFrame < 0) ? true : false;

			FilepathResultText.text = $"Depth file read! ModelTypeVal: {modelTypeVal}";
		}
		else {
			_framecount = 0; //set on update
			_startFrame = 0; //should be reset in the handler
			_vp.sendFrameReadyEvents = true; //have vp send events when frame is ready -- so that we can catch the first frame & check if the first frame starts with 0
		}

		/* Load the video -- should be done after hash is computed */
		_vp.url = filepath;

		_currentFrame = -1;
	}

	private void SaveDepth(bool shouldReload=false) {
		if (_processedFrames == null || _processedFrames.Count <= 0) return;
		//if (_orig_width*_orig_height*_x*_y == 0) return;

		/* Wait */
		StatusText.text = "Saving.";
		Task.WaitAll(_processedFrames.ToArray());

		if (shouldReload) {
			StatusText.text = "Reloading the depthsfile...";
			DepthFileUtils.Reopen();
		}
	}

	public void GetBuiltInModel() {
		_donnx = _depthModelBehavior.GetBuiltIn();
	}
	
	private void OnVideoError(VideoPlayer vp, string message) {
		FilepathResultText.text = "Failed to load video: " + message;
		vp.Stop();
		vp.url = "";
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
		const string pythonTarget = @"./depthpy/depth.py";

		string isVideo = (_currentFileType == FileTypes.Vid) ? " -v " : " ";

		int modelTypeVal = (int) modelType;
		string modelTypeString = modelType.ToString();

		string depthFilename = DepthFileUtils.GetDepthFileName(Path.GetFileName(_orig_filepath), modelTypeVal, _hashval);

		System.Diagnostics.Process.Start(pythonPath, $" \"{pythonTarget}\" \"{_orig_filepath}\" \"{depthFilename}\" {isVideo} -t {modelTypeString} --zip_in_memory");
	}

	public void HideUI() {
		UI.SetActive(!UI.activeSelf);
	}

	/* 3 functions below are copy-paseted (for now) */

	public void SetDepthMult() {
		float rat = DepthMultSlider.value;
		/* Depth has to be updated when the image is being shown */
		bool shouldUpdate = (_currentFileType == FileTypes.Img);

		_meshBehavior.SetDepthMult(rat, shouldUpdate);
	}

	public void SetAlpha() {
		float rat = AlphaSlider.value;
		/* Depth has to be updated when the image is being shown */
		bool shouldUpdate = (_currentFileType == FileTypes.Img);

		_meshBehavior.SetAlpha(rat, shouldUpdate);
	}

	public void SetBeta() {
		float rat = BetaSlider.value;
		/* Depth has to be updated when the image is being shown */
		bool shouldUpdate = (_currentFileType == FileTypes.Img);

		_meshBehavior.SetBeta(rat, shouldUpdate);
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

	public void OpenOutputFolder() {
		Application.OpenURL(Application.persistentDataPath);
	}

	public void BrowseFiles() {
		string[] paths = StandaloneFileBrowser.OpenFilePanel("Open File", "", _extFilters, false);
		if (paths.Length < 1)
			return;
		string path = paths[0];

		FilepathInputField.text = path;
		SelectFile();
	}

	public void ShowAboutScreen() {
		AboutScreen.SetActive(true);
	}

	public void CloseAboutScreen() {
		AboutScreen.SetActive(false);
	}
} 
