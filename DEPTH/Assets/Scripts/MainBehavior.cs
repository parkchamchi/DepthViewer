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

	public Slider DepthMultSlider;
	public Slider AlphaSlider;
	public Slider BetaSlider;
	public Slider MeshLocSlider;
	public Slider ScaleSlider;

	public TMP_InputField FilepathInputField;
	public TMP_Text FilepathResultText;

	public TMP_Text StatusText;

	public Toggle OutputSaveToggle;
	public TMP_Text OutputSaveText;

	public GameObject UI;

	public GameObject AboutScreen;
	public TMP_Text AboutText;
	public TextAsset AboutTextAsset;

	public GameObject DepthFilePanel;
	public TMP_Text DepthFileCompareText;

	public TMP_Text DesktopRenderToggleButtonText;

	public enum FileTypes {
		NotExists, 
		Dir,
		Img, Vid,
		Depth,
		Desktop,
		Unsupported
	};

	public static readonly string[] SupportedImgExts = {
		".jpg", ".png", //tested
	};
	public static readonly string[] SupportedVidExts = {
		".mp4", //tested
		".asf", ".avi", ".dv", ".m4v", ".mov", ".mpg", ".mpeg", ".ogv", ".vp8", ".webm", ".wmv"
	};
	public static readonly string[] SupportedDepthExts = {DepthFileUtils.DepthExt};

	private FileTypes _currentFileType = FileTypes.NotExists;

	private MeshBehavior _meshBehav;
	private DepthModelBehavior _depthModelBehav;
	private DepthONNX _donnx;
	private VRRecordBehavior _vrRecordBehav;
	private DesktopRenderBehavior _desktopRenderBehav;

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

	private bool _canUpdateArchive; //User option, toggled in UI; variable below overrides this
	private bool _shouldUpdateArchive;
	private bool _hasCreatedArchive;
	private List<Task> _processedFrames;

	private ExtensionFilter[] _extFilters;

	/* For depthfile input */
	private bool _recording;
	private bool _shouldCapture;
	private string _recordPath;

	void Start() {
		_meshBehav = GameObject.Find("DepthPlane").GetComponent<MeshBehavior>();
		_depthModelBehav = GameObject.Find("DepthModel").GetComponent<DepthModelBehavior>();
		GetBuiltInModel();
		_vrRecordBehav = GameObject.Find("VRRecord").GetComponent<VRRecordBehavior>();
		_desktopRenderBehav = GameObject.Find("DesktopRender").GetComponent<DesktopRenderBehavior>();

		_vp = GameObject.Find("Video Player").GetComponent<VideoPlayer>();
		_vp.frameReady += OnFrameReady;
		_vp.errorReceived += OnVideoError;
		_vp.loopPointReached += OnLoopPointReached;

		ToggleOutputSave(); //initializing _canUpdadeArchive

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
		string[] exts = new string[SupportedImgExts.Length + SupportedVidExts.Length + SupportedDepthExts.Length];
		int idx = 0;
		for (int i = 0; i < SupportedImgExts.Length; i++)
			exts[idx++] = SupportedImgExts[i].Substring(1, SupportedImgExts[i].Length-1);
		for (int i = 0; i < SupportedVidExts.Length; i++)
			exts[idx++] = SupportedVidExts[i].Substring(1, SupportedVidExts[i].Length-1);
		for (int i = 0; i < SupportedDepthExts.Length; i++)
			exts[idx++] = SupportedDepthExts[i].Substring(1, SupportedDepthExts[i].Length-1);

		_extFilters = new [] {
			new ExtensionFilter("Image/Video/Depth Files", exts),
		};

		/* Set about screen */
		CloseAboutScreen(); //redundant
		AboutText.text = AboutTextAsset.text;

		DepthFilePanel.SetActive(false);
	}

	void Update() {
		if (Input.GetMouseButtonDown(1))
			HideUI();

		if (_currentFileType == FileTypes.Vid && _vp != null)
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
			if (_shouldUpdateArchive)
				_processedFrames.Add(Task.Run(() => DepthFileUtils.UpdateDepthFile(depths, actualFrame, _x, _y)));

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
		//ShowAboutScreen();

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
		_processedFrames.Clear();
		
		_startFrame = _currentFrame = _framecount = 0;
		_x = _y = _orig_width = _orig_height = 0;
		DepthFilePanel.SetActive(false);

		_shouldUpdateArchive = false;
		_hasCreatedArchive = false;
		OutputSaveText.text = "";

		_recording = false;
		_shouldCapture = false;

		DesktopRenderToggleButtonText.text = "Run";

		_meshBehav.ShouldUpdateDepth = false; //only true in images
	}

	public void SelectFile() {
		/*
			Check if the depth file exists & load it if if exists.
			If new image/video was selected and the previous one was a video, save it.
		*/

		string filepath = FilepathInputField.text;
		FileTypes ftype = GetFileType(filepath);

		if (_currentFileType == FileTypes.Depth) {
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

		if (ftype == FileTypes.Img || ftype == FileTypes.Vid) {
			StatusText.text = "Hashing.";
			_hashval = Utils.GetHashval(filepath);
			StatusText.text = "Hashed.";
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
			OnImageError(filepath);
			return;
		}

		_meshBehav.ShouldUpdateDepth = true;

		_orig_width = texture.width;
		_orig_height = texture.height;

		//For metadata
		_startFrame = 0;

		int modelTypeVal;
		float[] depths;

		//Check if the file was processed
		_depthFilePath = DepthFileUtils.ProcessedDepthFileExists(_hashval, out modelTypeVal);
		if (_depthFilePath != null) {
			DepthFileUtils.ReadDepthFile(_depthFilePath, out _framecount, out _metadata, readOnlyMode: true);
			depths = DepthFileUtils.ReadFromArchive(0, out _x, out _y);

			FilepathResultText.text = $"Depth file read! ModelTypeVal: {modelTypeVal}";
			StatusText.text = "read from archive";

			OutputSaveText.text = "Full."; //Image depth file is implicitly full.
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

			StatusText.text = "processed";
		}

		_meshBehav.SetScene(depths, _x, _y, (float) _orig_width/_orig_height, texture);
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
		bool hashvalEquals = (_hashval == Utils.GetHashval(_orig_filepath));
		DepthFileCompareText.text += (hashvalEquals) ? "Hashval equals.\n" : "HASHVAL DOES NOT EQUAL.\n";

		_recordPath = $"{Application.persistentDataPath}/recordings/{Utils.GetTimestamp()}";
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

	public void DepthFileStartRecording() {
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

	public void DesktopRenderingToggle() {
		if (!_desktopRenderBehav.Supported) {
			Debug.LogError("StartDesktopRendering() called when !_desktopRenderBehav.Supported");
			return;
		}

		bool isRunning = (_currentFileType == FileTypes.Desktop);

		Cleanup(); //This sets _currentFileType. All tasks needed for stopping is handled here.

		if (!isRunning) {
			/* Start */
			_currentFileType = FileTypes.Desktop;
			DesktopRenderToggleButtonText.text = "Stop";
		}
	}

	private void DesktopRenderingUpdate() {
		Texture texture = _desktopRenderBehav.Get(out _orig_width, out _orig_height);
		if (texture == null) {
			Debug.LogError("Couldn't get the desktop screen");
			return;
		}

		if (_donnx == null) return;

		float[] depths = _donnx.Run(texture, out _x, out _y);
		_meshBehav.SetScene(depths, _x, _y, (float) _orig_width/_orig_height, texture);
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

	public void GetBuiltInModel() {
		_donnx = _depthModelBehav.GetBuiltIn();
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

		const string pythonPath = @"python"; //todo: change
		const string pythonTarget = @"./depthpy/depth.py";

		string isImage = (_currentFileType == FileTypes.Img) ? " -i " : " ";

		int modelTypeVal = (int) modelType;
		string modelTypeString = modelType.ToString();

		string depthFilename = DepthFileUtils.GetDepthFileName(Path.GetFileName(_orig_filepath), modelTypeVal, _hashval);

		System.Diagnostics.Process.Start(pythonPath, $" \"{pythonTarget}\" \"{_orig_filepath}\" \"{depthFilename}\" {isImage} -t {modelTypeString} --zip_in_memory");
	}

	public void HideUI() {
		UI.SetActive(!UI.activeSelf);
	}

	public void SetDepthMult() =>
		_meshBehav.DepthMult = DepthMultSlider.value;

	public void SetAlpha() =>
		_meshBehav.Alpha = AlphaSlider.value;

	public void SetBeta() =>
		_meshBehav.Beta = BetaSlider.value;

	public void SetMeshLoc() =>
		_meshBehav.MeshLoc = MeshLocSlider.value;

	public void SetScale() =>
		_meshBehav.Scale = ScaleSlider.value;

	public void ToDefault() {
		DepthMultSlider.value = MeshBehavior.DefaultDepthMult;
		AlphaSlider.value = MeshBehavior.DefaultAlpha;
		BetaSlider.value = MeshBehavior.DefaultBeta;
		MeshLocSlider.value = MeshBehavior.DefaultMeshLoc;
		ScaleSlider.value = MeshBehavior.DefaultScale;

		_meshBehav.ResetRotation();
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
