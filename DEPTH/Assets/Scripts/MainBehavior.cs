using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using SFB;
using IngameDebugConsole;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

#if UNITY_WEBGL
using System.Runtime.InteropServices; //Dllimport
using UnityEngine.Networking; //UnityWebRequest
#elif UNITY_ANDROID
using SimpleFileBrowser;
#endif

public enum FileTypes {
	NotExists, Dir, Unsupported,

	Img, Vid, Depth, //These 3 are heavily dependent on Unity components and interwined
	Online,
	Gif,
	Pgm,
};

public class MainBehavior : MonoBehaviour {

	public TMP_InputField FilepathInputField;

	public Toggle OutputSaveToggle;
	public Toggle SearchCacheToggle;

	public GameObject UI;

	public GameObject BrowseDirPanel;
	public TMP_Text BrowseDirText;
	public Toggle BrowseDirRandomToggle;
	public Toggle BrowseDirGifToggle;

	public GameObject OptionsScrollView; //To check if it is active; if it is, mousewheel will not be used for traversing files for BrowseDir

	public Toggle IsVideoToggle; //Only for WebGL. Automatically destroys itself otherwise.
	public GameObject WebXRSet; //same as above

	public Light MainLight;

	private FileTypes _currentFileType = FileTypes.NotExists;
	private TexInputs _texInputs;

	private MeshBehavior _meshBehav;
	private DepthModelBehavior _depthModelBehav;
	private DepthModel _donnx;
	private VRRecordBehavior _vrRecordBehav;
	private ServerConnectBehavior _serverBehav;
	private DesktopRenderBehavior _desktopRenderBehav;

	private VideoPlayer _vp;

	private bool _searchCache;
	private bool _canUpdateArchive; //User option, toggled in UI; can be ignored

	private ExtensionFilter[] _extFilters;

	private string[] _dirFilenames; //set by BrowseDir()
	private int _dirFileIdx;
	private bool _dirRandom = false;
	private List<int> _dirRandomIdxList;
	private int _dirGifCount; //number of gif files of the dir.

#if UNITY_WEBGL
	private bool _isVideo;
#endif

	void Start() {
		_meshBehav = GameObject.Find("DepthPlane").GetComponent<MeshBehavior>();
		_depthModelBehav = GameObject.Find("DepthModel").GetComponent<DepthModelBehavior>();
		_vrRecordBehav = GameObject.Find("VRRecord").GetComponent<VRRecordBehavior>();
		_serverBehav = GameObject.Find("ServerConnect").GetComponent<ServerConnectBehavior>();
		_desktopRenderBehav = GameObject.Find("DesktopRender").GetComponent<DesktopRenderBehavior>();

		_vp = GameObject.Find("Video Player").GetComponent<VideoPlayer>();

		ToggleOutputSave(); //initializing _canUpdateArchive
		ToggleSearchCache(); //init. _searchCache

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
		DebugLogConsole.AddCommandInstance("httpinput", "Get images from a url", "HttpOnlineTexStart", this);
		DebugLogConsole.AddCommandInstance("load_builtin", "Load the built-in model", "LoadBuiltIn", this);
		DebugLogConsole.AddCommandInstance("load_model", "Load ONNX model from path", "LoadModel", this);
		DebugLogConsole.AddCommandInstance("send_msg", "Send a message to _texInputs", "SendMsgToTexInputs", this);

		DebugLogConsole.AddCommandInstance("print_model_type", "Print the current model", "PrintCurrentModelType", _depthModelBehav);
		DebugLogConsole.AddCommandInstance("set_onnxruntime_params", "Set arguments for OnnxRuntime", "SetOnnxRuntimeParams", _depthModelBehav);

		//Load the built-in model: Not using the LoadBuiltIn() since that needs other components to be loaded
		_donnx = _depthModelBehav.GetBuiltIn();
	}

	void Update() {
		if (Input.GetMouseButtonDown(1))
			HideUI();

		if (_dirFilenames != null && Input.mouseScrollDelta.y != 0 && (OptionsScrollView == null || !OptionsScrollView.activeSelf)) //null check for OptionsScrollView is not needed
			SetBrowseDir(Input.mouseScrollDelta.y < 0);

		if (_texInputs != null)
			_texInputs.UpdateTex();
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

	public void Quit() {
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

		Debug.Log("Disposed.");

		Application.Quit();
	}

	public void CheckFileExists() {
		string filepath = FilepathInputField.text;
		FileTypes ftype = GetFileType(filepath);
		string output = "DEBUG: Default value, should not be seen.";

		/*
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
		case FileTypes.Gif:
			output = "GIF file.";
			break;
		case FileTypes.Unsupported:
		default:
			output = "Unsupported.";
			break;
		}
		*/

		output = $"Type: {ftype}";

		UITextSet.FilepathResultText.text = output;
	}

	public void SelectFile() {
		/*
			Check if the depth file exists & load it if if exists.
			If new image/video was selected and the previous one was a video, save it.
		*/

		if (_serverBehav.IsWaiting) {
			UITextSet.StatusText.text = "Waiting for the server...";
			return;
		}

		string filepath = FilepathInputField.text;
		FileTypes ftype = GetFileType(filepath);

		if (_texInputs != null && _texInputs.SeqInputBehav != null && _texInputs.SeqInputBehav.WaitingSequentialInput) {
			/* Selecting the texture for depthfile */
			_texInputs.SeqInputBehav.SequentialInput(filepath, ftype);
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
		OnlineTexStart(_desktopRenderBehav);

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

		_donnx = _depthModelBehav.GetBuiltIn();

		Debug.Log("Loaded the built-in model.");
	}

	//Mant to be used with the console
	public void LoadModel(string onnxpath, bool useOnnxRuntime=false) {
		Cleanup();
		_donnx?.Dispose();

		UITextSet.StatusText.text = "RELOAD";

		Debug.Log($"Loading model: {onnxpath}");

		string modelTypeStr = onnxpath;
		if (useOnnxRuntime) {
			modelTypeStr += ":OnnxRuntime";

			if (_depthModelBehav.OnnxRuntimeUseCuda)
				modelTypeStr += $":CUDA on {_depthModelBehav.OnnxRuntimeGpuId}";
		}

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

	public void SendMsgToTexInputs(string msg) {
		if (_texInputs == null) {
			Debug.LogError("_texInputs == null");
			return;
		}

		_texInputs.SendMsg(msg);
	}

	/* Does not work */
	/*
	public void RecenterVR() {
		//UnityEngine.XR.InputTracking.Recenter();
		var xrss = XRGeneralSettings.Instance?.Manager?.activeLoader?.GetLoadedSubsystem<XRInputSubsystem>();
		if (xrss == null) return;

		xrss.TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);
		xrss.TryRecenter();
	}
	*/
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
		ExtsDict.Add(FileTypes.Pgm, new string[] {".pgm"});

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
			Debug.LogError($"WebGLExts(): got illega fype {ftype}");
			return null;
		}
	}
}