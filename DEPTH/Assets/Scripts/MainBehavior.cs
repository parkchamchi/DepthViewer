using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using IngameDebugConsole;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

#if UNITY_WEBGL
using System.Runtime.InteropServices; //Dllimport
using UnityEngine.Networking; //UnityWebRequest
#endif

public enum FileTypes {
	NotExists, Dir, Unsupported,

	Img, Vid, Depth, //These 3 are heavily dependent on Unity components and interwined
	Online,
	Gif,
	Pgm,
};

public class MainBehavior : MonoBehaviour {

	//TODO: seperate the file selection
	//TODO: seperate the UI (Toggle, ...)

	public TMP_InputField FilepathInputField;

	public Toggle OutputSaveToggle;
	public Toggle SearchCacheToggle;

	public GameObject UI;

	public GameObject BrowseDirPanel;
	public TMP_Text BrowseDirText;
	public Toggle BrowseDirRandomToggle;
	public Toggle BrowseDirGifToggle;

	public GameObject OptionsScrollView; //To check if it is active; if it is, mousewheel will not be used for traversing files for BrowseDir

	public GameObject WebXRSet; //same as above

	private FileTypes _currentFileType = FileTypes.NotExists;
	private TexInputs _texInputs;

	private MeshBehavior _meshBehav;
	private DepthModelBehavior _depthModelBehav;
	private DepthModel _donnx;
	private VRRecordBehavior _vrRecordBehav;
	private ServerConnectBehavior _serverBehav;

	private VideoPlayer _vp;

	private bool _searchCache;
	private bool _canUpdateArchive; //User option, toggled in UI; can be ignored

	private KeyCode[] _sendMsgKeyCodes; //When a key in the array is pressed, it is sent to _texInputs using SendMsg().

	private FileSelecter _fileSelecter;

	private string[] _dirFilenames; //set by BrowseDir()
	private int _dirFileIdx;
	private List<int> _dirRandomIdxList;
	private int _dirGifCount; //number of gif files of the dir.
	private bool _dirRandom = false;

	public bool DirRandom {
		get => _dirRandom;

		set {
			//Avoid the recursive call
			if (_dirRandom == value) return;
			_dirRandom = value;

			if (value) {//reshuffle
				Debug.Log("Shuffle is on.");
				ShuffleBrowseDirRandomIdxList();
			}
			else {
				Debug.Log("Shuffle is off.");

				//shuffle -> noshuffle: set the index to be what is currently being shown
				if (_dirFilenames != null && _dirFilenames.Length >= 0) {
					int idx = System.Array.FindIndex(_dirFilenames, (x) => x == FilepathInputField.text);
					
					if (idx >= 0)
						_dirFileIdx = idx;
				}
			}

			if (BrowseDirRandomToggle.isOn != value)	
				BrowseDirRandomToggle.isOn = value;
		}
	}

	void Start() {
		_meshBehav = GameObject.Find("DepthPlane").GetComponent<MeshBehavior>();
		_depthModelBehav = GameObject.Find("DepthModel").GetComponent<DepthModelBehavior>();
		_vrRecordBehav = GameObject.Find("VRRecord").GetComponent<VRRecordBehavior>();
		_serverBehav = GameObject.Find("ServerConnect").GetComponent<ServerConnectBehavior>();

		_vp = GameObject.Find("Video Player").GetComponent<VideoPlayer>();

		ToggleOutputSave(); //initializing _canUpdateArchive
		ToggleSearchCache(); //init. _searchCache

		/* Check the first arguement */
		string[] args = System.Environment.GetCommandLineArgs();
		if (args.Length > 1) {
			string arg = args[1];
			FileTypes argFileType = GetFileType(arg);

			if (argFileType == FileTypes.Img || argFileType == FileTypes.Vid) {
				SelectFile(arg);
			}
		}

		/* If this key is pressed, _texInputs is informed. */
		//Not used anymore.
		_sendMsgKeyCodes = new KeyCode[] {
			
		};

		_fileSelecter = new StandaloneFileSelecter();

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

		/*Console methods*/
		AddCommandDelegate addcmd = DebugLogConsole.AddCommandInstance;

		addcmd("httpinput", "Get images from a url", "HttpOnlineTexStart", this);
		addcmd("load_builtin", "Load the built-in model", "LoadBuiltIn", this);
		addcmd("load_model", "Load ONNX model from path", "LoadModel", this);
		addcmd("send_msg", "Send a message to _texInputs", "SendMsgToTexInputs", this);
		addcmd("set_mousemove", "Whether the mesh would follow the mouse", "SetMoveMeshByMouse", this);
		addcmd("set_fileselecter", "Select the file selecter (standalone, simple)", "SetFileSelecter", this);
		addcmd("set_dof", "Set the DoF [3, 6]", "SetDof", this);
		addcmd("zmq", "Load the ZeroMQ model", "LoadZmqModel", this);

		addcmd("wiggle", "Rotate the mesh in a predefined manner", "Wiggle", this);
		addcmd("wiggle4", "Rotate the mesh in a predefined manner (4 vars)", "Wiggle4", this);
		addcmd("stopwiggle", "Stop wiggling", "StopWiggle", this);

		addcmd("e", "Save the parameters for image/video inputs (on the first frame, force) (shorthand for `send_msg e`)", "SendMsgE", this);
		addcmd("ec", "Save the parameters for image/video inputs (on the current frame) (shorthand for `send_msg ec`)", "SendMsgEc", this);
		addcmd("ecf", "Save the parameters for image/video inputs (on the current frame, force) (shorthand for `send_msg ecf`)", "SendMsgEcf", this);
		addcmd("eclear", "Clear the parameters for image/video inputs (shorthand for `send_msg eclear`)", "SendMsgEclear", this);

		addcmd("print_model_metadata", "Print the metadata of the current model (only supports ORT)", "PrintCurrentModelMetadata", this);
		addcmd("set_ort_gpuid", "Set the id of the GPU. default: 0", "SetOrtGpuId", this);
		addcmd("set_ort_settings", "Set the settings string for GPU execution provider. default: null. Type \"null\" for the null value.", "SetOrtGpuSettings", this);

		addcmd("dbg", "Temporary method for debugging.", "DebugTmp", this);
		addcmd("vrmode", "Enter VR mode (incomplete, controls won't work)", "EnterVrMode", this);

		//Load the built-in model: Not using the LoadBuiltIn() since that needs other components to be loaded
		_donnx = _depthModelBehav.GetBuiltIn();

		//Try to load the options
		try {
			MeshSliderParents.ImportMinMax();

			bool searchCache, saveOutput;
			Utils.ReadOptionsString(out searchCache, out saveOutput);
			SearchCacheToggle.isOn = searchCache;
			OutputSaveToggle.isOn = saveOutput;
		}
		catch (Exception exc) {
			Debug.LogError($"Failed to load the options: {exc}");
		}
	}

	void Update() {
		/* Hide the UI */
		if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(Keymapper.Inst.HideUI))
			HideUI();

		/* Scroll the directory */
		//Check if the scroll key is in
		bool shouldScroll = false;
		bool scrollDirection = true;

		if (Keymapper.Inst.UseMouseWheel && Input.mouseScrollDelta.y != 0) {
			shouldScroll = true;
			scrollDirection = Input.mouseScrollDelta.y < 0;
		}
		else if (Input.GetKeyDown(Keymapper.Inst.PrevFileInDir)) {
			shouldScroll = true;
			scrollDirection = false;
		}
		else if (Input.GetKeyDown(Keymapper.Inst.NextFileInDir)) {
			shouldScroll = true;
			scrollDirection = true;
		}
		else {
			shouldScroll = false; //not needed
		}

		if (_dirFilenames != null && shouldScroll && (OptionsScrollView == null || !UI.activeSelf || !OptionsScrollView.activeSelf)) //null check for OptionsScrollView is not needed
			SetBrowseDir(scrollDirection);

		/* Toggle shuffle */
		if (Input.GetKeyDown(Keymapper.Inst.ToggleShuffle))
			DirRandom = !DirRandom;

		/* Send the key to _texInputs */
		if (_texInputs != null) {
			_texInputs.UpdateTex();

			foreach (var key in _sendMsgKeyCodes)
				if (Input.GetKeyDown(key))
					SendMsgToTexInputs(key.ToString());
		}
		else
			UITextSet.StatusText.text = "Input is not set. (See console `)";
	}

	private void Cleanup() {
		/* Called by SelectFile(), DesktopRenderingStart() */

		_texInputs?.Dispose();
		_texInputs = null;
		DepthFileUtils.Dispose(); //not needed, should be handled at _texInputs.Dispose()

		_currentFileType = FileTypes.Unsupported;
		
		UITextSet.OutputSaveText.text = "";
		UITextSet.StatusText.text = "";
		UITextSet.FilepathResultText.text = "";

		_meshBehav.ShouldUpdateDepth = false; //only true in images
	}

	void OnApplicationQuit() {
#if UNITY_WEBGL
		StatusText.text = "Quitting.";
#endif

		Cleanup();

		if (_vp != null)
			Destroy(_vp);

		_donnx?.Dispose();
		_donnx = null;

		if (_meshBehav != null)
			Destroy(_meshBehav);

		//Try to save the options
		try {
			MeshSliderParents.ExportMinMax();

			Utils.WriteOptionsString(SearchCacheToggle.isOn, OutputSaveToggle.isOn);
		}
		catch (Exception e) {
			//will not be shown on the build
			Debug.LogError($"Error saving the options: {e}");
		}

		Debug.Log("Disposed.");		
	}

	public void Quit() =>
		Application.Quit();

	public void CheckFileExists() {
		string filepath = FilepathInputField.text;
		FileTypes ftype = GetFileType(filepath);

		string output = $"Type: {ftype}";
		UITextSet.FilepathResultText.text = output;
	}

	public void OnFilepathEntered() =>
		SelectFile(FilepathInputField.text);

	public void SelectFile(string filepath) {
		/*
			Check if the depth file exists & load it if if exists.
			If new image/video was selected and the previous one was a video, save it.
		*/
		//TODO: Uncouple tihs with `FilepathInputField`

		if (_serverBehav.IsWaiting) {
			UITextSet.StatusText.text = "Waiting for the server...";
			return;
		}

		FilepathInputField.text = filepath;
		FileTypes ftype = GetFileType(filepath);

		if (_texInputs != null && _texInputs.WaitingSequentialInput) {
			/* Selecting the texture for depthfile */
			_texInputs.SequentialInput(filepath, ftype);
			return;
		}

		bool isSupportedType = false;
		var supportedFileTypes = new FileTypes[] {FileTypes.Img, FileTypes.Vid, FileTypes.Depth, FileTypes.Gif, FileTypes.Pgm};
		foreach (var t in supportedFileTypes)
			if (ftype == t) {
				isSupportedType = true;
				break;
			}
		if (!isSupportedType)
			return;

		Cleanup();

		_currentFileType = ftype;
		UITextSet.FilepathResultText.text = $"Current Type: {ftype}";

		switch (ftype) {
		case FileTypes.Img:
		case FileTypes.Vid:
		case FileTypes.Depth:
			_texInputs = new ImgVidDepthTexInputs(_currentFileType, _meshBehav, _donnx, filepath, _searchCache, _canUpdateArchive, _vp, _vrRecordBehav, _serverBehav);
			break;
		case FileTypes.Gif:
			_texInputs = new GifTexInputs(filepath, _donnx, _meshBehav);
			break;
		case FileTypes.Pgm:
			_texInputs = new PgmTexInputs(filepath, _meshBehav);
			break;
		default:
			UITextSet.StatusText.text = "DEBUG: SelectFile(): something messed up :(";
			Debug.LogError($"SelectFile(): ftype {ftype} is in supportedFileTypes but not implemented");
			break;
		}
	}

	public static FileTypes GetFileType(string filepath) {
		bool file_exists = File.Exists(filepath);
		bool dir_exists = Directory.Exists(filepath);

		if (!file_exists && !dir_exists)
			return FileTypes.NotExists;

		if (dir_exists)
			return FileTypes.Dir;

		return Exts.FileTypeCheck(filepath);
	}

	public void DesktopRenderingStart() =>
		OnlineTexStart(GameObject.Find("DesktopRender").GetComponent<DesktopRenderBehavior>()); //deprecated

	public void HttpOnlineTexStart(string url) =>
		OnlineTexStart(new HttpOnlineTex(url));

	private void OnlineTexStart(OnlineTex otex) {
		if (!otex.Supported) {
			Debug.LogError("OnlineTexStart() called when !otex.Supported");
			return;
		}
		if (_serverBehav.IsWaiting) {
			UITextSet.StatusText.text = "Waiting for the server.";
			return;
		}

		Cleanup(); //This sets _currentFileType. All tasks needed for stopping is handled here.
		ClearBrowseDir();

		_currentFileType = FileTypes.Online;
		_texInputs = new OnlineTexInputs(_donnx, _meshBehav, otex);
	}

	public void LoadBuiltIn() {
		Cleanup();
		_donnx?.Dispose();

		UITextSet.StatusText.text = "RELOAD";

		_donnx = _depthModelBehav.GetBuiltIn();

		Debug.Log("Loaded the built-in model.");
	}

	//Mant to be used with the console
	public void LoadModel(string onnxpath, bool useOnnxRuntime=false) {
		Cleanup();
		_donnx?.Dispose();

		UITextSet.StatusText.text = "RELOAD";

		Debug.Log($"Loading model: {onnxpath}");

		string modelTypeStr = Path.GetFileNameWithoutExtension(onnxpath);

		try {
			_donnx = _depthModelBehav.GetDepthModel(onnxpath, modelTypeStr, useOnnxRuntime: useOnnxRuntime);
		}
		catch (Exception exc) {
			_donnx?.Dispose();
			_donnx = null;

			Debug.LogError("LoadModel(): Got exception: " + exc);
		}

		//Failure
		if (_donnx == null) {
			Debug.LogError($"Failed to load: {modelTypeStr}");
			return;
		}

		Debug.Log($"Loaded the model: {modelTypeStr}");
	}

	public void LoadZmqModel(int port=5555) {
		Cleanup();
		_donnx?.Dispose();

		UITextSet.StatusText.text = "RELOAD";

		_donnx = _depthModelBehav.GetZmqDepthModel(port, () => {
			Cleanup();
			_donnx = null;
			UITextSet.StatusText.text = "ZMQ DISPOSED";
		});
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
		Application.OpenURL(DepthFileUtils.SaveDir);
	}

/* Implementations of BrowseFiles() */
#if !(UNITY_WEBGL && !UNITY_EDITOR)

	public void BrowseFiles() =>
		_fileSelecter.SelectFile((path) => {
			ClearBrowseDir();
			SelectFile(path);
		});

#elif UNITY_WEBGL && !UNITY_EDITOR //#else

	public void BrowseFiles() =>
		_fileSelecter.SelectFile((path) => {
			Cleanup();
			_currentFileType = FileTypes.Img;
			StartCoroutine(GetRequest(new System.Uri(url).AbsoluteUri));
		});

	IEnumerator GetRequest(string uri) {
		using (UnityWebRequest webRequest = UnityWebRequest.Get(uri)) {
			// Request and wait for the desired page.
			yield return webRequest.SendWebRequest();

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
					Depth depth = _donnx.Run(texture);
					_meshBehav.SetScene(depth, texture);

					break;
			}
		}
	}

#endif

	public void ToggleBrowseDirPanel() =>
		WindowManager.SetCurrentWindow(BrowseDirPanel);

	public void OnBrowseDirRandomToggleValueChanged() {
		//Called when the toggle on UI is changed
		DirRandom = BrowseDirRandomToggle.isOn;
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
			int randomidx = UnityEngine.Random.Range(i, _dirRandomIdxList.Count);

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

		if (_serverBehav.IsWaiting) {
			UITextSet.StatusText.text = "Waiting the server.";
			return;
		}

		_dirFileIdx += (next) ? +1 : -1;
		_dirFileIdx = (_dirFileIdx % _dirFilenames.Length);
		if (_dirFileIdx < 0) _dirFileIdx += _dirFilenames.Length;

		int idx = (_dirRandom) ? _dirRandomIdxList[_dirFileIdx] : _dirFileIdx;
		string newfilename = _dirFilenames[idx];

		if (!BrowseDirGifToggle.isOn && Exts.FileTypeCheck(newfilename) == FileTypes.Gif) {
			/* Skip GIF files */

			/* When the directory only has Gif files */
			if (_dirGifCount == _dirFilenames.Length)
				return;

			SetBrowseDir(next);
			return;
		}

		SelectFile(newfilename);
	}

public void SetBrowseDirName(string dirname) {
	if (!Directory.Exists(dirname)) {
		Debug.LogError($"BrowseDirs() callback: dir {dirname} does not exist. This shouldn't be seen when using the file browser...");
		return;
	}

	int gifcount = 0; //number of the gif files in the directory.

	//Add only: img, vid, gif
	List<string> filenames_list = new List<string>();
	foreach (string filename in Directory.GetFiles(dirname)) {
		FileTypes ftype = Exts.FileTypeCheck(filename);
		if (ftype == FileTypes.Img || ftype == FileTypes.Vid || ftype == FileTypes.Gif) {
			filenames_list.Add(filename);

			if (ftype == FileTypes.Gif)
				gifcount++;
		}
	}

	if (filenames_list.Count == 0)
		return;
	if (!BrowseDirGifToggle.isOn && (gifcount == filenames_list.Count)) //directory with only gif files
		return;

	BrowseDirText.text = dirname;
	_dirFilenames = filenames_list.ToArray();
	_dirFileIdx = 0;
	_dirGifCount = gifcount;

	ShuffleBrowseDirRandomIdxList();

	SetBrowseDir();
}

/* Implementations of BrowseDirs() */
#if UNITY_STANDALONE || UNITY_EDITOR
	//Also used by Keymapper.cs
	public void BrowseDirs() =>
		_fileSelecter.SelectDir(SetBrowseDirName);

#else
	public void BrowseDirs() {
		Debug.LogError("Not implemented.");
		return;
	}
#endif

	public void SendMsgToTexInputs(string msg) {
		if (_texInputs == null) {
			Debug.LogError("_texInputs == null");
			return;
		}

		_texInputs.SendMsg(msg);
	}

	public void SendMsgE() => SendMsgToTexInputs("e");
	public void SendMsgEc() => SendMsgToTexInputs("ec");
	public void SendMsgEcf() => SendMsgToTexInputs("ecf");
	public void SendMsgEclear() => SendMsgToTexInputs("eclear");

	public void MeshToDefault() =>
		_meshBehav.ToDefault();

	public void SetTargetValToNaN() =>
		_meshBehav.TargetVal = System.Single.NaN;

	public void SetMeshShader(string shadername) {
		MeshShaders meshShader;

		switch (shadername) {
		case "standard":
			meshShader = MeshShaders.GetStandard();
			break;
		case "pointCloudDisk":
			meshShader = MeshShaders.GetPointCloudDisk();
			break;
		case "pointCloudPoint":
			meshShader = MeshShaders.GetPointCloudPoint();
			break;
		default:
			Debug.LogError($"SetMeshShader(): Got unknown shader name {shadername}");
			return;
		}

		_meshBehav.SetShader(meshShader);
	}

	public void SetPointCloudSize(float val) =>
		_meshBehav.SetMaterialFloat("_PointSize", val);

	public void SetOrtGpuProvider(string provider) =>
		_depthModelBehav.OnnxRuntimeGpuProvider = provider;

	public void SetOrtGpuId(int gpuId) =>
		_depthModelBehav.OnnxRuntimeGpuId = gpuId;

	public void SetOrtGpuSettings(string settings) =>
		_depthModelBehav.OnnxRuntimeGpuSettings = (settings == "null") ? null : settings;

	public string GetCurrentModelType() {
		if (_donnx == null)
			return "null";

		return _donnx.ModelType;
	}

	public void PrintCurrentModelMetadata() =>
		_donnx?.PrintMetadata();

	public void SetMoveMeshByMouse(bool value) {
		Debug.Log($"Setting MoveMeshByMouse = {value}");
		_meshBehav.MoveMeshByMouse = value;
	}

	public void SetMeshTextureSetCallback(bool val, System.Action<Texture> callback=null) {
		/*
		Set the callback the mesh will call when the texture is ready.
		Used for the skybox.
		*/

		if (val) {
			Debug.Log("Setting the skybox.");

			//Have the camera see the skybox
			Camera camera = GameObject.Find("MainCamera").GetComponent<Camera>();
			camera.clearFlags = CameraClearFlags.Skybox;

			_meshBehav.OnTextureSet = callback;
		}

		else {
			Debug.Log("Disabling the skybox.");

			Camera camera = GameObject.Find("MainCamera").GetComponent<Camera>();
			camera.clearFlags = CameraClearFlags.SolidColor;
			_meshBehav.OnTextureSet = null;
			RenderSettings.skybox = null;
		}
	}

	public bool IsMeshTextureCallbackSet() =>
		_meshBehav.OnTextureSet != null;

	public Depth GetCurrentDepth(DepthMapType type) =>
		_meshBehav.GetDepth(type);

	public void GetCurrentTextureSize(out int w, out int h) =>
		_meshBehav.GetTextureSize(out w, out h);

	public void Wiggle(float intervalScale, float horAngle, float verAngle) {
		Debug.Log($"Wiggling: {intervalScale}, ({horAngle}, {verAngle})");
		_meshBehav.MeshWiggler = new Wiggler(intervalScale, horAngle, verAngle);
	}

	public void Wiggle4(float intervalScale, float leftAngle, float rightAngle, float upAngle, float downAngle) {
		Debug.Log($"Wiggling: {intervalScale}, ({leftAngle}, {rightAngle}, {upAngle} {downAngle})");
		_meshBehav.MeshWiggler = new Wiggler(intervalScale, leftAngle, rightAngle, upAngle, downAngle);
	}

	public void StopWiggle() {
		Debug.Log("Stopping the wiggling movement...");
		_meshBehav.MeshWiggler = null;
	}

	public void SetFileSelecter(string fileSelecter) {
		Debug.Log($"SetFileSelecter: {fileSelecter}");

		switch (fileSelecter) {
		case "standalone":
			_fileSelecter = new StandaloneFileSelecter();
			return;
		case "simple":
			_fileSelecter = new SimpleFileSelecter();
			return;
		default:
			Debug.LogError($"Got unknown fileSelecter {fileSelecter}");
			return;
		}
	}

	public void EnterVrMode() {
		Debug.Log("VR mode (incomplete)");

		SetFileSelecter("simple");
		GameObject.Find("Canvas").GetComponent<TempCanvasBehavior>().VrMode();
	}

	public void SetDof(int dof) {
		Camera camera = GameObject.Find("MainCamera").GetComponent<Camera>();
		UnityEngine.SpatialTracking.TrackedPoseDriver tpd = GameObject.Find("MainCamera").GetComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>();

		switch (dof) {
		case 3:
			camera.transform.localPosition = new Vector3(0, 0, 0);
			tpd.trackingType = UnityEngine.SpatialTracking.TrackedPoseDriver.TrackingType.RotationOnly;
			break;
		case 6:
			tpd.trackingType = UnityEngine.SpatialTracking.TrackedPoseDriver.TrackingType.RotationAndPosition;
			break;
		default:
			Debug.LogError($"Invalid DoF: {dof}");
			return;
		}

		Debug.Log($"DoF: {dof}");
	}

	/* A method for debugging, called by the console method `dbg` */
	public void DebugTmp() {
		Debug.Log("DebugTmp() called.");

		Debug.Log("Nothing here...");

		Debug.Log("DebugTmp() exiting.");
	}
} 

public static class Exts {
	public static Dictionary<FileTypes, string[]> ExtsDict {get; private set;}

	//with no '.'
	public static string[] AllExtsWithoutDot {get; private set;}
	public static string[] AllExtsWithDot {get; private set;}

	static Exts() {
		ExtsDict = new Dictionary<FileTypes, string[]>();

		ExtsDict.Add(FileTypes.Img, new string[] {".jpg", ".jpeg", ".png"});
		ExtsDict.Add(FileTypes.Vid, new string[] {
			".mp4",
			".asf", ".avi", ".dv", ".m4v", ".mov", ".mpg", ".mpeg", ".ogv", ".vp8", ".webm", ".wmv"
		});
		ExtsDict.Add(FileTypes.Depth, new string[] {DepthFileUtils.DepthExt});
		ExtsDict.Add(FileTypes.Gif, new string[] {".gif"});
		ExtsDict.Add(FileTypes.Pgm, new string[] {".pgm"});

		List<string> allExtsWithDotList = new List<string>();
		List<string> allExtsWithoutDotList = new List<string>();

		foreach (string[] exts in ExtsDict.Values)
			foreach (string ext in exts) {
				allExtsWithDotList.Add(ext);
				allExtsWithoutDotList.Add(ext.Substring(1, ext.Length-1));
			}

		AllExtsWithDot = allExtsWithDotList.ToArray();
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
			Debug.LogError($"WebGLExts(): got illega fype {ftype}");
			return null;
		}
	}
}