using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using SFB;

using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using TMPro;

#if UNITY_WEBGL
using System.Runtime.InteropServices; //Dllimport
using UnityEngine.Networking; //UnityWebRequest
#elif UNITY_ANDROID
using SimpleFileBrowser;
#endif

public enum FileTypes {
		NotExists, 
		Dir,
		Img, Vid,
		Depth,
		Desktop,
		Gif,
		Unsupported
	};

public class MainBehavior : MonoBehaviour {

	public TMP_InputField FilepathInputField;
	public TMP_Text FilepathResultText;

	public TMP_Text StatusText;

	public Toggle OutputSaveToggle;
	public Toggle SearchCacheToggle;
	public TMP_Text OutputSaveText;

	public GameObject UI;

	public GameObject DepthFilePanel;
	public TMP_Text DepthFileCompareText;

	public GameObject BrowseDirPanel;
	public TMP_Text BrowseDirText;
	public Toggle BrowseDirRandomToggle;

	public GameObject OptionsScrollView; //To check if it is active; if it is, mousewheel will not be used for traversing files for BrowseDir

	public Toggle IsVideoToggle; //Only for WebGL. Automatically destroys itself otherwise.
	public GameObject WebXRSet; //same as above

	public Light MainLight;

	private string _savedir;
	public string SaveDir {
		set {
			if (!Directory.Exists(value)) {
				Debug.LogError("Invalid directory: " + value);
				return;
			}
			
			_savedir = value;
			DepthFileUtils.DepthDir = $"{_savedir}/depths";
		}
		get {
			return _savedir;
		}
	}

	public GameObject CallPythonObjectParent; //Only visible when the hashval is set
	private string _pythonPath = "python";
	public string PythonPath {set {_pythonPath = value;}}

	public Toggle CallServerOnPauseToggle;

	private FileTypes _currentFileType = FileTypes.NotExists;

	private MeshBehavior _meshBehav;
	private DepthModelBehavior _depthModelBehav;
	private DepthModel _donnx;
	private VRRecordBehavior _vrRecordBehav;
	private DesktopRenderBehavior _desktopRenderBehav;
	private ServerConnectBehavior _serverBehav;

	private int _x, _y;
	private int _orig_width, _orig_height;

	private string _orig_filepath;
	private string _hashval;

	private VideoPlayer _vp;
	private long _startFrame;
	private long _currentFrame;
	private long _framecount;
	private string _depthFilePath; //path to the depth file read for a video, null if not exists.
	private Dictionary<string, string> _metadata;

	private bool _searchCache;
	private bool _canUpdateArchive; //User option, toggled in UI; variable below overrides this
	private bool _shouldUpdateArchive;
	private bool _hasCreatedArchive;
	private List<Task> _processedFrames;

	private bool _waitingServer = false;
	private Texture _serverTexture; //input texture for the server

	private bool _desktopRenderPaused;

	private ExtensionFilter[] _extFilters;

	/* For depthfile input */
	private bool _recording;
	private bool _shouldCapture;
	private string _recordPath;

	private string[] _dirFilenames; //set by BrowseDir()
	private int _dirFileIdx;
	private bool _dirRandom = false;
	private List<int> _dirRandomIdxList;

#if UNITY_WEBGL
	private bool _isVideo;
#endif

	void Start() {
		_meshBehav = GameObject.Find("DepthPlane").GetComponent<MeshBehavior>();
		_depthModelBehav = GameObject.Find("DepthModel").GetComponent<DepthModelBehavior>();
		GetBuiltInModel();
		_vrRecordBehav = GameObject.Find("VRRecord").GetComponent<VRRecordBehavior>();
		_desktopRenderBehav = GameObject.Find("DesktopRender").GetComponent<DesktopRenderBehavior>();
		_serverBehav = GameObject.Find("ServerConnect").GetComponent<ServerConnectBehavior>();

		SaveDir = Application.persistentDataPath;

		_vp = GameObject.Find("Video Player").GetComponent<VideoPlayer>();
		_vp.frameReady += OnFrameReady;
		_vp.errorReceived += OnVideoError;
		_vp.loopPointReached += OnLoopPointReached;

		ToggleOutputSave(); //initializing _canUpdateArchive
		ToggleSearchCache(); //init. _searchCache

		_processedFrames = new List<Task>();

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
		_extFilters = new [] {
			new ExtensionFilter("Image/Video/Depth Files", Exts.AllExtsWithoutDot),
		};

		DepthFilePanel.SetActive(false);

#if UNITY_WEBGL && !UNITY_EDITOR
		_canUpdateArchive = false;
		_searchCache = false;

		/* File browsing for WebGL */
		IsVideoToggle.gameObject.SetActive(true);

		WebXRSet.SetActive(true);

#elif UNITY_ANDROID && !UNITY_EDITOR

		FileBrowser.SetFilters(false, new FileBrowser.Filter("Image/Video/Depth Files", Exts.AllExtsWithoutDot));
		Screen.sleepTimeout = SleepTimeout.NeverSleep;
		Screen.brightness = 1.0f;
#endif
	}

	void Update() {
		if (Input.GetMouseButtonDown(1))
			HideUI();

		if (_dirFilenames != null && Input.mouseScrollDelta.y != 0 && (OptionsScrollView == null || !OptionsScrollView.activeSelf)) //null check for OptionsScrollView is not needed
			SetBrowseDir(Input.mouseScrollDelta.y < 0);

		else if (_currentFileType == FileTypes.Vid && _vp != null)
			UpdateVideoDepth();	

		else if (_currentFileType == FileTypes.Depth && _recording && _shouldCapture)
			DepthFileCaptureFrame();

		else if (_currentFileType == FileTypes.Desktop)
			DesktopRenderingUpdate();
	}

	private void OnFrameReady(VideoPlayer vp, long frame) {
		/* 
		This handler only gets invoked once for the first frame.
		parameter `frame` is the index of the first frame,
		which is usually 0, but some video files start with 1 (or more).
		For _currentFrame, subtract this so that the first frame is always 0.
		*/
		/*
		Also called when _recording, after _startFrame is set.
		*/

		if (_currentFileType == FileTypes.Depth && _recording) {
			DepthFileFrameReady();
			return;
		}

		_startFrame = frame;
		_framecount = (long) vp.frameCount;

		/* Set original width/height & framecount for first time */
		_orig_width = (int) vp.width;
		_orig_height = (int) vp.height;
		
		//Disable
		vp.sendFrameReadyEvents = false;

		if (_currentFileType == FileTypes.Depth) {
			vp.Stop();
			DepthFileAfterFirstFrame();
		}
	}

	private void OnLoopPointReached(VideoPlayer vp) {
		vp.Stop();

		/* Does not work (why?)
		if (_currentFileType == FileTypes.Depth && _recording) {
			DepthFileEnded();
			return;
		}*/

		SaveDepth(shouldReload: _shouldUpdateArchive);

		/* Now read the saved depths */
		if (_depthFilePath == null && _hasCreatedArchive) {
			_depthFilePath = DepthFileUtils.ProcessedDepthFileExists(_hashval, out _);
		}

		vp.Play();
	}

	private void UpdateVideoDepth() {
		if (_currentFileType != FileTypes.Vid && _currentFileType != FileTypes.Depth) return;
	
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
			depths = DepthFileUtils.ReadFromArchive(actualFrame, out _x, out _y);

		if (depths != null) 
			StatusText.text = "read from archive";
		else {
			//Run the model
			if (_donnx == null) return;
			depths = _donnx.Run(texture, out _x, out _y);

			/* For a new media, create the depth file */
			if (_depthFilePath == null && !_hasCreatedArchive && _shouldUpdateArchive) {
				DepthFileUtils.CreateDepthFile(_framecount-_startFrame, _startFrame, _hashval, _orig_filepath, _orig_width, _orig_height, _x, _y, _donnx.ModelTypeVal);
				_hasCreatedArchive = true;
			}

			//Save it
			if (_shouldUpdateArchive) {
				depths = (float[]) depths.Clone();
				_processedFrames.Add(Task.Run(() => DepthFileUtils.UpdateDepthFile(depths, actualFrame, _x, _y)));
			}

			StatusText.text = "processed";
		}

		if (_currentFileType == FileTypes.Depth)
			StatusText.text = $"#{actualFrame}/{_framecount-_startFrame}";
		
		_meshBehav.SetScene(depths, _x, _y, (float) _orig_width/_orig_height, texture);
	}

	public void HaltVideo() {
		if (_vp == null) return;
		_vp.Stop();
		_vp.url = null;
	}

	public void Quit() {
#if UNITY_WEBGL
		StatusText.text = "Quitting.";
#endif

		SaveDepth(); //save the current one
		HaltVideo();
		DepthFileUtils.Dispose();

		_currentFileType = FileTypes.Unsupported;

		if (_vp != null)
			Destroy(_vp);

		if (_donnx != null)
			_donnx.Dispose();
		_donnx = null;

		if (_meshBehav != null)
			Destroy(_meshBehav);

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

	void Cleanup() {
		/* Called by SelectFile(), DesktopRenderingStart() */

		SaveDepth();
		HaltVideo();
		DepthFileUtils.Dispose();

		_orig_filepath = null;
		_currentFileType = FileTypes.Unsupported;

		_hashval = null;
		_depthFilePath = null;
		_processedFrames.Clear();
		
		_startFrame = _currentFrame = _framecount = 0;
		_x = _y = _orig_width = _orig_height = 0;
		DepthFilePanel.SetActive(false);

		if (CallPythonObjectParent != null)
			CallPythonObjectParent.SetActive(false);

		_shouldUpdateArchive = false;
		_hasCreatedArchive = false;
		OutputSaveText.text = "";
		StatusText.text = "";

		_recording = false;
		_shouldCapture = false;

		_meshBehav.ShouldUpdateDepth = false; //only true in images

		_waitingServer = false;
		_serverTexture = null;
		_desktopRenderPaused = false;
	}

	public void SelectFile() {
		/*
			Check if the depth file exists & load it if if exists.
			If new image/video was selected and the previous one was a video, save it.
		*/

		if (_waitingServer) {
			StatusText.text = "Waiting for the server...";
			return;
		}

		string filepath = FilepathInputField.text;
		FileTypes ftype = GetFileType(filepath);

		if (_currentFileType == FileTypes.Depth && !_recording) {
			/* Selecting the texture for depthfile */
			DepthFileInput(filepath, ftype);
			return;
		}

		if (ftype != FileTypes.Img && ftype != FileTypes.Vid && ftype != FileTypes.Depth) return;

		Cleanup();

		_currentFileType = ftype;
		_orig_filepath = filepath;

		if (_shouldUpdateArchive = _canUpdateArchive) //assign & compare
			OutputSaveText.text = "Will be saved.";
		else
			OutputSaveText.text = "Won't be saved.";

		if (_searchCache || _shouldUpdateArchive) {
			if (ftype == FileTypes.Img || ftype == FileTypes.Vid) {
				StatusText.text = "Hashing.";
				_hashval = Utils.GetHashval(filepath);
				StatusText.text = "Hashed.";

				if (CallPythonObjectParent != null)
					CallPythonObjectParent.SetActive(true);
			}
		}

		switch (ftype) {
		case FileTypes.Img:
			FromImage(filepath);
			break;
		case FileTypes.Vid:
			FromVideo(filepath);
			break;
		case FileTypes.Depth:
			FromDepthFile(filepath);
			break;
		}
	}

	public static FileTypes GetFileType(string filepath) {
		if (!File.Exists(filepath)) 
			return FileTypes.NotExists;

		if (Directory.Exists(filepath))
			return FileTypes.Dir;

		return Exts.FileTypeCheck(filepath);
	}

	private void FromImage(string filepath) {
		Texture texture = Utils.LoadImage(filepath);

		/* Couldn't load */
		if (texture == null) {
			OnImageError(filepath);
			return;
		}

		FromImage(texture);
	}

	private void FromImage(Texture texture) {
		if (texture == null) {
			OnImageError("");
			return;
		}

		_meshBehav.ShouldUpdateDepth = true;

		_orig_width = texture.width;
		_orig_height = texture.height;

		//For metadata
		_startFrame = 0;
		_framecount = 1;

		int modelTypeVal = -1;
		float[] depths = null;

		//Check if the file was processed
		if (_searchCache)
			_depthFilePath = DepthFileUtils.ProcessedDepthFileExists(_hashval, out modelTypeVal);
		else
			_depthFilePath = null; //redundant, set in Cleanup()

		if (_depthFilePath != null) {
			DepthFileUtils.ReadDepthFile(_depthFilePath, out _framecount, out _metadata, readOnlyMode: true);
			depths = DepthFileUtils.ReadFromArchive(0, out _x, out _y);

			FilepathResultText.text = $"Depth file read! ModelTypeVal: {modelTypeVal}";
			StatusText.text = "read from archive";

			OutputSaveText.text = "Full."; //Image depth file is implicitly full.
		}

		else if (_serverBehav.IsAvailable) {
			/* This will be processed some frames later  */
			_waitingServer = true;
			_serverTexture = texture; 
			_serverBehav.Run(_serverTexture, (float[] depths, int x, int y) => {
				if (!OnDepthReady(depths, x, y))
					return false;
	
				if (_shouldUpdateArchive) {
					DepthFileUtils.CreateDepthFile(_framecount, _startFrame, _hashval, _orig_filepath, _orig_width, _orig_height, x, y, _serverBehav.ModelTypeVal, model_type: _serverBehav.ModelType);

					//depths = (float[]) depths.Clone();
					_processedFrames.Add(Task.Run(() => DepthFileUtils.UpdateDepthFile(depths, 0, x, y)));
					_hasCreatedArchive = true; //not needed
				}

				return true; //return value does not matter here
			});
			return;
		}

		else {
			depths = _donnx.Run(texture, out _x, out _y);

			/* Save */
			if (_shouldUpdateArchive) {
				DepthFileUtils.CreateDepthFile(_framecount, _startFrame, _hashval, _orig_filepath, _orig_width, _orig_height, _x, _y, _donnx.ModelTypeVal);

				depths = (float[]) depths.Clone();
				_processedFrames.Add(Task.Run(() => DepthFileUtils.UpdateDepthFile(depths, 0, _x, _y)));
				_hasCreatedArchive = true; //not needed
			}

			StatusText.text = "processed";
		}

		_meshBehav.SetScene(depths, _x, _y, (float) _orig_width/_orig_height, texture);
	}

	private bool OnDepthReady(float[] depths, int x, int y) {
		/* returns true when `depths` is valid */

		if (_waitingServer == false) //Cleanup()
			return false;
		_waitingServer = false;

		if (depths == null || x == 0 || y == 0) {
			StatusText.text = "DepthServer error";
			return false;
		}

		if (_serverTexture == null) {
			Debug.LogError("OnDepthReady(): _serverTexture == null");
			return false;
		}
	
		_meshBehav.SetScene(depths, x, y, (float) _orig_width/_orig_height, _serverTexture);
		StatusText.text = "From DepthServer";

		_serverTexture = null;
		return true;
	}

	public void HaltWaitingServer() =>
		_waitingServer = false;

	private void FromVideo(string filepath) {
		/* _orig_width, _orig_height, & framecount should be set when the frame is recieved!*/

		int modelTypeVal = -1;

		/* Check if the processed file exists */
		if (_searchCache)
			_depthFilePath = DepthFileUtils.ProcessedDepthFileExists(_hashval, out modelTypeVal);
		else
			_depthFilePath = null; //redundant, set in Cleanup()

		if (_depthFilePath != null) {
			//Should not save if the loaded depth's modeltypeval is higher than the program is using
			if (modelTypeVal != _donnx.ModelTypeVal) {//for now just use !=
				_shouldUpdateArchive = false;
				OutputSaveText.text = "Not saving.";
			}

			bool isFull = DepthFileUtils.ReadDepthFile(_depthFilePath, out _framecount, out _metadata, readOnlyMode: !_shouldUpdateArchive);
			if (isFull)
				OutputSaveText.text = "Full.";

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

	/************************************************************************************/
	/* Depth file input
	/************************************************************************************/

	private void FromDepthFile(string filepath) {
		StatusText.text = "INPUT TEXTURE";

		_shouldUpdateArchive = false;
		_recording = false;
		
		OutputSaveText.text = "";
	}

	private void DepthFileInput(string textureFilepath, FileTypes ftype) {
		/*
		`_orig_filepath` holds the path to the depthfile.
		*/

		/* Invalid filetypes */
		if (ftype != FileTypes.Img && ftype != FileTypes.Vid) {
			StatusText.text = "INVALID INPUT.";
			DepthFilePanel.SetActive(false);
			_currentFileType = FileTypes.Unsupported;
			return;
		}

		StatusText.text = "INPUT READ.";

		_depthFilePath = _orig_filepath;
		_orig_filepath = textureFilepath;

		/* Read the depthfile */
		DepthFileUtils.ReadDepthFile(_depthFilePath, out _, out _metadata, readOnlyMode: true); //let _framecount be read from the texture input
		_hashval = _metadata["hashval"]; //_hashval will use the metadata

		/*
		Should Check:
			hashval
			framecount
			isfull
		*/

		DepthFileCompareText.text = "";

		/* Check hashval */
#if !UNITY_WEBGL
		bool hashvalEquals = (_hashval == Utils.GetHashval(_orig_filepath));
		DepthFileCompareText.text += (hashvalEquals) ? "Hashval equals.\n" : "HASHVAL DOES NOT EQUAL.\n";
#endif

		/* Show modeltypeval */
		string modelTypeVal = _metadata["model_type_val"];
		DepthFileCompareText.text += $"Model type val: {modelTypeVal}\n";

		_recordPath = $"{SaveDir}/recordings/{Utils.GetTimestamp()}";
		Utils.CreateDirectory(_recordPath);
	
		if (ftype == FileTypes.Vid) {
			/* Check framecount */
			//Load the video
			_vp.sendFrameReadyEvents = true;
			_currentFrame = -1;
			_vp.url = _orig_filepath; //This sets _framecount

			return;
		}
		else {
			/* Image input */
			Texture texture = Utils.LoadImage(_orig_filepath);
			if (texture == null) {
				OnImageError(_orig_filepath);
				return;
			}

			_meshBehav.ShouldUpdateDepth = true;

			_orig_width = texture.width;
			_orig_height = texture.height;
			_framecount = 1;

			float[] depths = DepthFileUtils.ReadFromArchive(0, out _x, out _y);
			_meshBehav.SetScene(depths, _x, _y, (float) _orig_width/_orig_height, texture);

			DepthFilePanel.SetActive(true);
		}
	}

	private void DepthFileAfterFirstFrame() {
		/* Called after the first frame is received, so that _framecount is set. */

		/* Check framecount */
		long framecount_metadata = long.Parse(_metadata["framecount"]);
		long actual_framecount_input = (_framecount - _startFrame);
		long framecountDelta = (actual_framecount_input - framecount_metadata);

		if (framecountDelta == 0) {
			DepthFileCompareText.text += $"Framecount equals: ({_framecount})\n";
		}
		else if (framecountDelta < 0) { //depthfile has more frames -> leave it be
			DepthFileCompareText.text += $"FRAMECOUNT DOES NOT EQUAL: (depth > input) : ({framecount_metadata}:{actual_framecount_input})\n";
		}
		else {
			DepthFileCompareText.text += $"FRAMECOUNT DOES NOT EQUAL: (depth < input) : ({framecount_metadata}:{actual_framecount_input})\n";
			DepthFileCompareText.text += $"-> #{framecountDelta} FRAMES WILL BE TRIMMED.\n";
			
			_framecount -= framecountDelta;
		}

		/* Check if the depth file is full */
		bool isDepthFull = DepthFileUtils.IsFull();
		DepthFileCompareText.text += (isDepthFull) ? "Depthfile is full.\n" : "DEPTHFILE IS NOT FULL.\n";

		DepthFilePanel.SetActive(true);
	}

	public void DepthFileStartRecording(int size=2048) {
		_vrRecordBehav.Size = size;

		if (_framecount <= 1) {
			//Image --> capture and exit (scene is already set)
			DepthFileCapture();
			StatusText.text = "Captured!";

			DepthFilePanel.SetActive(false);
			_currentFileType = FileTypes.Unsupported;
			return; //code below will not execused.
		}

		/* Record per frame */
		_recording = true;
		_shouldCapture = false;
		
		DepthFilePanel.SetActive(false);

		_vp.sendFrameReadyEvents = true;
		_vp.Play();
	}

	private void DepthFileFrameReady() {
		_vp.Pause();
		UpdateVideoDepth();

		/* Let the mesh update */
		_shouldCapture = true;
	}

	private void DepthFileCaptureFrame() {
		DepthFileCapture();
		_shouldCapture = false;

		if (_currentFrame+1 >= _framecount) {
			/* Manually end it, since loopPointReached does not work and it loops for some reason */
			DepthFileEnded();
		}
		else
			//_vp.Play();
			_vp.frame++;
	}

	private void DepthFileCapture(string format="jpg") {
		_processedFrames.Add(_vrRecordBehav.Capture($"{_recordPath}/{_currentFrame-_startFrame}.{format}", format));
	}

	private void DepthFileEnded() {
		_recording = false;
		_shouldCapture = false;

		_vp.sendFrameReadyEvents = false;
		_vp.Stop();
		_vp.url = "";

		Task.WaitAll(_processedFrames.ToArray());
		_processedFrames.Clear();

		_currentFileType = FileTypes.Unsupported;

		StatusText.text = "DONE.";
	}

	/* Switch to video */
	public void DepthFileShow() {
		if (_currentFileType != FileTypes.Depth) {
			Debug.LogError("_currentFileType != FileTypes.Depth");
			return;
		}

		DepthFilePanel.SetActive(false);
		OutputSaveText.text = "Not saving.";

		if (_framecount > 1) {
			_currentFileType = FileTypes.Vid;
			_vp.Play();
		}
		else
			_currentFileType = FileTypes.Img;
	}

	/************************************************************************************/
	/* End - Depth file input
	/************************************************************************************/

	public void DesktopRenderingStart() {
		if (!_desktopRenderBehav.Supported) {
			Debug.LogError("StartDesktopRendering() called when !_desktopRenderBehav.Supported");
			return;
		}
		if (_waitingServer) {
			StatusText.text = "Waiting for the server.";
			return;
		}

		Cleanup(); //This sets _currentFileType. All tasks needed for stopping is handled here.
		ClearBrowseDir();

		_currentFileType = FileTypes.Desktop;
		_desktopRenderBehav.StartRendering();
	}

	private void DesktopRenderingUpdate() {
		if (_desktopRenderPaused)
			return;

		Texture texture = _desktopRenderBehav.Get(out _orig_width, out _orig_height);
		if (texture == null) {
			Debug.LogError("Couldn't get the desktop screen");
			return;
		}

		if (_donnx == null) return;

		float[] depths = _donnx.Run(texture, out _x, out _y);
		_meshBehav.SetScene(depths, _x, _y, (float) _orig_width/_orig_height, texture);

		_serverTexture = texture;
	}

	private void SaveDepth(bool shouldReload=false) {
		//if (_processedFrames == null || _processedFrames.Count <= 0) return;
		/*
		It appears that if the zip archive is opened as update, it is illegal to read entry.length even if no modification is done.
		So no matter _processed is empty, depthfile has to be Reopen()'d if shouldReload == true.
		*/
		if (_processedFrames == null) {
			Debug.LogError("SaveDepth() called when _processedFrames == null");
			return;
		}

		if (_processedFrames.Count <= 0 && !shouldReload) return;

		/* Wait */
		StatusText.text = "Saving."; //Does not work (should be called in update)
		Task.WaitAll(_processedFrames.ToArray());
		_processedFrames.Clear();

		/* Check if it is full */
		bool isFull = DepthFileUtils.IsFull();
		if (isFull) {
			_shouldUpdateArchive = false;
			OutputSaveText.text = "Now full.";
		}

		if (shouldReload) {
			StatusText.text = "Reloading the depthsfile...";
			DepthFileUtils.Reopen();
		}
	}

	public void SetModel(DepthModel model) {
		_donnx?.Dispose();
		_donnx = model;
	}

	public void GetBuiltInModel() =>
		SetModel(_depthModelBehav.GetBuiltIn());

	public void PausePlayVideo() {
		if (_currentFileType == FileTypes.Vid) {
			if (_vp == null) return;

			if (_vp.isPaused) {
				/* Play */
				if (_waitingServer) {
					StatusText.text = "Waiting for the server.";
					return;
				}

				_meshBehav.ShouldUpdateDepth = false;
				_vp.Play();
			}
			else {
				/* Pause */
				_vp.Pause();

				if (_serverBehav.IsAvailable && CallServerOnPauseToggle != null && CallServerOnPauseToggle.isOn) {
					_waitingServer = true;
					_serverTexture = _vp.texture;

					_serverBehav.Run(_serverTexture, OnDepthReady);
				}

				_meshBehav.ShouldUpdateDepth = true;
			}
		}

		else if (_currentFileType == FileTypes.Desktop) {
			if (_desktopRenderPaused) {
				/* Unpause */
				if (_waitingServer) {
					StatusText.text = "Waiting for the server.";
					return;
				}

				_desktopRenderPaused = false;
				StatusText.text = "Unpaused.";

				_meshBehav.ShouldUpdateDepth = false;
			}
			else {
				/* Pause */
				_desktopRenderPaused = true;
				StatusText.text = "Paused.";

				//_serverTexture is always set
				if (_serverBehav.IsAvailable && CallServerOnPauseToggle != null && CallServerOnPauseToggle.isOn) {
					_waitingServer = true;
					_serverBehav.Run(_serverTexture, OnDepthReady);
				}

				_meshBehav.ShouldUpdateDepth = true;
			}
		}
	}
	
	private void OnVideoError(VideoPlayer vp, string message) {
		FilepathResultText.text = "Failed to load video: " + message;
		vp.Stop();
		vp.url = "";

		_currentFileType = FileTypes.Unsupported;
	}

	private void OnImageError(string filepath) {
		FilepathResultText.text = "Failed to load image: " + filepath;

		_currentFileType = FileTypes.Unsupported;
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

		const string pythonTarget = @"./depthpy/depth.py";

		string isImage = (_currentFileType == FileTypes.Img) ? " -i " : " ";

		int modelTypeVal = (int) modelType;
		string modelTypeString = modelType.ToString();

		string depthFilename = DepthFileUtils.GetDepthFileName(Path.GetFileName(_orig_filepath), modelTypeVal, _hashval);

		System.Diagnostics.Process.Start(_pythonPath, $" \"{pythonTarget}\" \"{_orig_filepath}\" \"{depthFilename}\" {isImage} -t {modelTypeString} --zip_in_memory");
	}

	public void HideUI() {
		UI.SetActive(!UI.activeSelf);
	}

	public void ToggleFullscreen() {
		if (Screen.fullScreenMode == FullScreenMode.Windowed)
			Screen.fullScreenMode = FullScreenMode.MaximizedWindow;
		else
			Screen.fullScreenMode = FullScreenMode.Windowed;
	}

	public void ToggleOutputSave() {
		_canUpdateArchive = OutputSaveToggle.isOn;
		
		if (_canUpdateArchive) { //turn on _searchCache too
			SearchCacheToggle.isOn = true;
			SearchCacheToggle.interactable = false;
		}
		else {
			SearchCacheToggle.interactable = true;
		}
	}

	public void ToggleSearchCache() {
		_searchCache = SearchCacheToggle.isOn;
	}

	public void OpenOutputFolder() {
		Application.OpenURL(_savedir);
	}

/* Implementations of BrowseFiles() */
#if UNITY_STANDALONE || UNITY_EDITOR

	public void BrowseFiles() {

		string[] paths = StandaloneFileBrowser.OpenFilePanel("Open File", "", _extFilters, false);
		if (paths.Length < 1)
			return;
		string path = paths[0];

		ClearBrowseDir();
		FilepathInputField.text = path;
		SelectFile();
	}
	
#elif UNITY_ANDROID

	//public static bool ShowSaveDialog(OnSuccess onSuccess, OnCancel onCancel, FileBrowser.PickMode pickMode, bool allowMultiSelection = false, string initialPath = null, string initialFilename = null, string title = "Save", string saveButtonText = "Save" );

	public void BrowseFiles() =>
		FileBrowser.ShowLoadDialog(OnFileUpload, null, FileBrowser.PickMode.Files);

	public void OnFileUpload(string[] paths) {
		string path = paths[0];
		FilepathInputField.text = path;
		SelectFile();
	}

#elif UNITY_WEBGL && !UNITY_EDITOR

	[DllImport("__Internal")]
	private static extern void UploadFile(string gameObjectName, string methodName, string filter, bool multiple);

	public void BrowseFiles() {
		_isVideo = IsVideoToggle.isOn;
		FileTypes ftype = (_isVideo) ? FileTypes.Vid : FileTypes.Img;
		string exts = Exts.WebGLExts(ftype);

		UploadFile(gameObject.name, "OnFileUpload", exts, false);
	}

	public void OnFileUpload(string url) {
		Cleanup();
		_orig_filepath = url; //not needed

		if (_isVideo) {
			_currentFileType = FileTypes.Vid;
			FromVideo(url);
		}
		else {
			_currentFileType = FileTypes.Img;
			StartCoroutine(GetRequest(new System.Uri(url).AbsoluteUri));
		}
	}

	IEnumerator GetRequest(string uri) {
		using (UnityWebRequest webRequest = UnityWebRequest.Get(uri)) {
			// Request and wait for the desired page.
			yield return webRequest.SendWebRequest();

			string[] pages = uri.Split('/');
			int page = pages.Length - 1;

			switch (webRequest.result) {
				case UnityWebRequest.Result.ConnectionError:
				case UnityWebRequest.Result.DataProcessingError:
					StatusText.text = ("Error: " + webRequest.error);
					break;
				case UnityWebRequest.Result.ProtocolError:
					StatusText.text = ("HTTP Error: " + webRequest.error);
					break;
				case UnityWebRequest.Result.Success:
					StatusText.text = ("Received");

					Texture2D texture = Utils.LoadImage(webRequest.downloadHandler.data);
					FromImage(texture);
					break;
			}
		}
	}

#endif

	public void ToggleBrowseDirPanel() {
		BrowseDirPanel.SetActive(!BrowseDirPanel.activeSelf);
	}

	public void ToggleBrowseDirRandom() {
		_dirRandom = BrowseDirRandomToggle.isOn;

		if (_dirRandom) //reshuffle
			ShuffleBrowseDirRandomIdxList();
		else {
			//shuffle -> noshuffle: set the index to be what is currently being shown
			if (_dirFilenames != null && _dirFilenames.Length >= 0) {
				int idx = System.Array.FindIndex(_dirFilenames, (x) => x == FilepathInputField.text);
				
				if (idx >= 0)
					_dirFileIdx = idx;
			}
		}
	}

	private void ShuffleBrowseDirRandomIdxList() {
		if (_dirFilenames == null) return;

		if (_dirRandomIdxList == null || _dirRandomIdxList.Count != _dirFilenames.Length) {
			_dirRandomIdxList = new List<int>();
			for (int i = 0; i < _dirFilenames.Length; i++)
				_dirRandomIdxList.Add(i);
		}

		/* Shuffle */
		for (int i = 0; i < _dirRandomIdxList.Count; i++) {
			int randomidx = Random.Range(i, _dirRandomIdxList.Count);

			int tmp = _dirRandomIdxList[i];
			_dirRandomIdxList[i] = _dirRandomIdxList[randomidx];
			_dirRandomIdxList[randomidx] = tmp;
		}
	}

	public void ClearBrowseDir() {
		_dirFilenames = null;
		_dirRandomIdxList = null;
		BrowseDirText.text = "";
	}

	private void SetBrowseDir(bool next=true) {
		if (_dirFilenames == null) {
			Debug.LogError("SetBrowseDir() called when _dirFilenames == null");
			return;
		}
		if (_dirFilenames.Length == 0)
			return;

		if (_waitingServer) {
			StatusText.text = "Waiting the server.";
			return;
		}

		_dirFileIdx += (next) ? +1 : -1;
		_dirFileIdx = (_dirFileIdx % _dirFilenames.Length);
		if (_dirFileIdx < 0) _dirFileIdx += _dirFilenames.Length;

		int idx = (_dirRandom) ? _dirRandomIdxList[_dirFileIdx] : _dirFileIdx;
		string newfilename = _dirFilenames[idx];
		FilepathInputField.text = newfilename;
		SelectFile();
	}

/* Implementations of BrowseDirs() */
#if UNITY_STANDALONE || UNITY_EDITOR
	public void BrowseDirs() {
		string[] dirnames = StandaloneFileBrowser.OpenFolderPanel("Select a directory", null, false);
		if (dirnames.Length < 1)
			return;
		string dirname = dirnames[0];

		//Add only: img, vid, gif
		List<string> filenames_list = new List<string>();
		foreach (string filename in Directory.GetFiles(dirname)) {
			FileTypes ftype = Exts.FileTypeCheck(filename);
			if (ftype == FileTypes.Img || ftype == FileTypes.Vid || ftype == FileTypes.Gif) {
				filenames_list.Add(filename);
			}
		}

		if (filenames_list.Count == 0)
			return;

		BrowseDirText.text = dirname;
		_dirFilenames = filenames_list.ToArray();
		_dirFileIdx = 0;

		ShuffleBrowseDirRandomIdxList();

		SetBrowseDir();
	}
#else
	public void BrowseDirs() {
		Debug.LogError("Not implemented.");
		return;
	}
#endif

	public void SetVideoSpeed(float mult) {
		if (_vp == null) {
			Debug.LogError("SetVideoSpeed() called when _vp == null");
			return;
		}

		_vp.playbackSpeed = mult;
	}

	public void SetMeshX(float val) =>
		_meshBehav.MeshX = val;

	public void SetMeshY(float val) =>
		_meshBehav.MeshY = val;

	public void SetLightIntensity(float val) =>
		MainLight.intensity = val;

	public void RecenterVR() {
		/* Does not work */
		//UnityEngine.XR.InputTracking.Recenter();
		var xrss = XRGeneralSettings.Instance?.Manager?.activeLoader?.GetLoadedSubsystem<XRInputSubsystem>();
		if (xrss == null) return;

		xrss.TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);
		xrss.TryRecenter();
	}
} 

public static class Exts {
	public static Dictionary<FileTypes, string[]> ExtsDict {get; private set;}

	//with no '.'
	public static string[] AllExtsWithoutDot {get; private set;}

	static Exts() {
		ExtsDict = new Dictionary<FileTypes, string[]>();

		ExtsDict.Add(FileTypes.Img, new string[] {".jpg", ".png"});
		ExtsDict.Add(FileTypes.Vid, new string[] {
			".mp4",
			".asf", ".avi", ".dv", ".m4v", ".mov", ".mpg", ".mpeg", ".ogv", ".vp8", ".webm", ".wmv"
		});
		ExtsDict.Add(FileTypes.Depth, new string[] {DepthFileUtils.DepthExt});
		ExtsDict.Add(FileTypes.Gif, new string[] {".gif"});

		List<string> allExtsWithoutDotList = new List<string>();
		foreach (string[] exts in ExtsDict.Values)
			foreach (string ext in exts)
				allExtsWithoutDotList.Add(ext.Substring(1, ext.Length-1));
		AllExtsWithoutDot = allExtsWithoutDotList.ToArray();
	}

	public static FileTypes FileTypeCheck(string filepath) {
		//Does not check if the file actually exists
		foreach (KeyValuePair<FileTypes, string[]> item in ExtsDict)
			foreach (string ext in item.Value)
				if (filepath.ToLower().EndsWith(ext))
					return item.Key;

		return FileTypes.Unsupported;
	}

	public static string WebGLExts(FileTypes ftype) {
		// e.g. ".jpg .png"

		if (!ExtsDict.ContainsKey(ftype)) {
			Debug.LogError($"WebGLExts: invalid ftype {ftype}");
			return "";
		}

		/* Why doesn't this work?
		string webglexts = "";
		foreach (string ext in ExtsDict[ftype])
			webglexts += (ext + " ");

		return webglexts.Substring(0, webglexts.Length-1);
		*/

		switch (ftype) {
		case FileTypes.Img:
			return "image/*";
		case FileTypes.Vid:
			return "video/*";
		/*case FileTypes.Gif:
			return ".gif";*/
		default:
			return null;
		}
	}
}