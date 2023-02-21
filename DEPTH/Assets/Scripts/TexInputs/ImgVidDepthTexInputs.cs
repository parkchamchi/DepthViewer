using System;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

public class ImgVidDepthTexInputs : TexInputs {

	public class TexInputBehav : SequentialInputBehav {
		private ImgVidDepthTexInputs _outer;
		public TexInputBehav(ImgVidDepthTexInputs outer) {_outer = outer;}

		public bool WaitingSequentialInput {
			get {return _outer._ftype == FileTypes.Depth && !_outer._recording;}
		}
		public void SequentialInput(string filepath, FileTypes ftype) {
			if (_outer._ftype != FileTypes.Depth ||_outer. _recording) {
				Debug.LogError($"SequentialInput() called when _ftype == {_outer._ftype} and _recording == {_outer._recording}");
				return;
			}

			_outer.DepthFileInput(filepath, ftype);
		}
	}
	private TexInputBehav _texInputBehav;
	public SequentialInputBehav SeqInputBehav {get {return _texInputBehav;}}

	private FileTypes _ftype;
	private DepthModel _dmodel;
	private IDepthMesh _dmesh;

	private IVRRecord _vrrecord;

	private AsyncDepthModel _asyncDmodel;
	private Texture _serverTexture; //input texture for the server

	private string _orig_filepath;
	private string _hashval;
	private int _x, _y; //is this needed?

	private bool _searchCache;
	private bool _canUpdateArchive; //user option; The params will be saved if this is `true`.
	private bool _shouldUpdateArchive; //Always `false` if `_canUpdateArchive` is `false`. If it's true the archive will be opened as mode `Update`, if not `Read`
	private bool _hasCreatedArchive;
	private List<Task> _processedFrames;

	private int _orig_width;
	private int _orig_height;

	private VideoPlayer _vp;

	private long _startFrame;
	private long _currentFrame;
	private long _framecount;
	private float _framerate; //for metadata

	private string _depthFilePath; //path to the depth file read for a video, null if not exists.
	private Dictionary<string, string> _metadata;

	/* For depthfile input */
	private string _depthfileCompareText;
	private int _recordSize; //if positive, it will generate a sequence of images
	private bool _recording;
	private bool _shouldCapture;
	private string _recordPath;

	/* Parameters for the mesh */
	private Dictionary<long, string> _paramsDict;
	private bool _paramsDictChanged;

	public ImgVidDepthTexInputs(
		FileTypes ftype,
		IDepthMesh dmesh,
		DepthModel dmodel,
		string filepath,

		bool searchCache, bool canUpdateArchive,
		VideoPlayer vp,
		IVRRecord vrrecord,

		AsyncDepthModel asyncDmodel=null
		)
	{
		if (ftype != FileTypes.Img && ftype != FileTypes.Vid && ftype != FileTypes.Depth) {
			Debug.LogError($"Got invalid ftype: {ftype}");
			return;
		}
		_ftype = ftype;
		_dmesh = dmesh;
		_dmodel = dmodel;
		_orig_filepath = filepath;

		_searchCache = searchCache;
		_shouldUpdateArchive = _canUpdateArchive = canUpdateArchive;
		_vrrecord = vrrecord;
		_asyncDmodel = asyncDmodel;

		_vp = vp;
		_vp.frameReady += OnFrameReady;
		_vp.errorReceived += OnVideoError;
		_vp.loopPointReached += OnLoopPointReached;

		_texInputBehav = new TexInputBehav(this);

		_paramsDictChanged = false;

		_processedFrames = new List<Task>();
		/* Img & Vid can be cached */
		if (ftype == FileTypes.Img || ftype == FileTypes.Vid) {
			if (_canUpdateArchive) //assign & compare
				UITextSet.OutputSaveText.text = "Will be saved.";
			else
				UITextSet.OutputSaveText.text = "Won't be saved.";

			if (_searchCache || _canUpdateArchive) {
				UITextSet.StatusText.text = "Hashing.";
				_hashval = Utils.GetHashval(filepath);
				UITextSet.StatusText.text = "Hashed.";
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

	public void FromImage(string filepath) {
		Texture texture = Utils.LoadImage(filepath);

		/* Couldn't load */
		if (texture == null) {
			OnImageError(filepath);
			return;
		}

		FromImage(texture);
	}

	public void FromImage(Texture texture) {
		if (texture == null) {
			OnImageError("");
			return;
		}

		_dmesh.ShouldUpdateDepth = true;

		_orig_width = texture.width;
		_orig_height = texture.height;

		//For metadata
		_startFrame = 0;
		_framecount = 1;

		float[] depths = null;

		//Check if the file was processed
		if (_searchCache)
			_depthFilePath = DepthFileUtils.ProcessedDepthFileExists(_hashval, _dmodel.ModelType);
		else
			_depthFilePath = null; //redundant, set in Cleanup()

		if (_depthFilePath != null) {
			string modelType;
			DepthFileUtils.ReadDepthFile(_depthFilePath, out _framecount, out modelType, out _metadata, out _paramsDict, readOnlyMode: true);
			depths = DepthFileUtils.ReadFromArchive(0, out _x, out _y);

			/*
				Import the params
				Between `-1` (init) and `0`, load the largest one
			*/
			foreach (var targetFrame in new long[] {-1, 0})
				if (_paramsDict != null && _paramsDict.ContainsKey(targetFrame))
					_dmesh.ImportParams(_paramsDict[targetFrame]);

			UITextSet.FilepathResultText.text = $"Depth file read! ModelType: {modelType}";
			UITextSet.StatusText.text = "read from archive";

			UITextSet.OutputSaveText.text = "Full."; //Image depth file is implicitly full.
		}

		else if (_asyncDmodel != null && _asyncDmodel.IsAvailable && !_asyncDmodel.IsWaiting) {
			/* This will be processed some frames later  */
			_serverTexture = texture; 
			_asyncDmodel.Run(_serverTexture, (float[] depths, int x, int y) => {
				if (!OnDepthReady(depths, x, y))
					return false;
	
				if (_shouldUpdateArchive) {
					DepthFileUtils.CreateDepthFile(_framecount, _startFrame, _hashval, _orig_filepath, _orig_width, _orig_height, _framerate, x, y, _asyncDmodel.ModelType);

					//depths = (float[]) depths.Clone();
					_processedFrames.Add(Task.Run(() => DepthFileUtils.UpdateDepthFile(depths, 0, x, y)));
					_hasCreatedArchive = true; //not needed
				}

				return true; //return value does not matter here
			});
			return;
		}

		else {
			depths = _dmodel.Run(texture, out _x, out _y);

			/* Save */
			if (_shouldUpdateArchive) {
				DepthFileUtils.CreateDepthFile(_framecount, _startFrame, _hashval, _orig_filepath, _orig_width, _orig_height, _framerate, _x, _y, _dmodel.ModelType);

				depths = (float[]) depths.Clone();
				_processedFrames.Add(Task.Run(() => DepthFileUtils.UpdateDepthFile(depths, 0, _x, _y)));
				_hasCreatedArchive = true; //not needed
			}

			UITextSet.StatusText.text = "processed";
		}

		_dmesh.SetScene(depths, _x, _y, (float) _orig_width/_orig_height, texture);

		
	}

	private void OnImageError(string filepath) {
		UITextSet.FilepathResultText.text = "Failed to load image: " + filepath;

		_ftype = FileTypes.Unsupported;
	}

	private bool OnDepthReady(float[] depths, int x, int y) {
		/* returns true when `depths` is valid */

		if (depths == null || x == 0 || y == 0) {
			UITextSet.StatusText.text = "DepthServer error";
			return false;
		}

		if (_serverTexture == null) {
			Debug.LogError("OnDepthReady(): _serverTexture == null");
			return false;
		}
	
		_dmesh.SetScene(depths, x, y, (float) _orig_width/_orig_height, _serverTexture);
		UITextSet.StatusText.text = "From DepthServer";

		_serverTexture = null;
		return true;
	}

	private void FromVideo(string filepath) {
		/* _orig_width, _orig_height, & framecount should be set when the frame is recieved!*/

		/* Check if the processed file exists */
		if (_searchCache)
			_depthFilePath = DepthFileUtils.ProcessedDepthFileExists(_hashval, _dmodel.ModelType);
		else
			_depthFilePath = null; //redundant, set in Cleanup()

		if (_depthFilePath != null) {
			string modelType;
			bool isFull = DepthFileUtils.ReadDepthFile(_depthFilePath, out _framecount, out modelType, out _metadata, out _paramsDict, readOnlyMode: !_shouldUpdateArchive);
			if (isFull)
				UITextSet.OutputSaveText.text = "Full.";

			//Set startframe also
			//It is set to negative if it couldn't be determined - in that case we should check it
			_startFrame = long.Parse(_metadata["startframe"]);
			_orig_width = int.Parse(_metadata["original_width"]);
			_orig_height = int.Parse(_metadata["original_height"]);
			_vp.sendFrameReadyEvents = (_startFrame < 0) ? true : false;

			//Check if the params exist on the init -- and if it does apply it
			try {
				if (_paramsDict != null && _paramsDict.ContainsKey(-1))
					_dmesh.ImportParams(_paramsDict[-1]);
			}
			catch (Exception exc) {
				Debug.LogError($"FromVideo(): error importing the parameters on init: {exc}");
			}

			UITextSet.FilepathResultText.text = $"Depth file read! ModelTypeVal: {modelType}";
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

	public void UpdateTex() {
		switch (_ftype) {
		case FileTypes.Vid:
			UpdateVid();
			break;
		case FileTypes.Depth:
			UpdateDepthfileInput();
			break;
		}
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

		if (_ftype == FileTypes.Depth && _recording) {
			DepthFileFrameReady();
			return;
		}

		_startFrame = frame;
		_framecount = (long) vp.frameCount;

		_framerate = vp.frameRate;

		/* Set original width/height & framecount for first time */
		_orig_width = (int) vp.width;
		_orig_height = (int) vp.height;
		
		//Disable
		vp.sendFrameReadyEvents = false;

		if (_ftype == FileTypes.Depth) {
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
			_depthFilePath = DepthFileUtils.ProcessedDepthFileExists(_hashval, _dmodel.ModelType);
		}

		vp.Play();
	}

	private void UpdateVid() {
		if (_ftype != FileTypes.Vid && _ftype != FileTypes.Depth) return;
		if (_vp == null) return;
	
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
		if (_depthFilePath != null) {
			try {
				depths = DepthFileUtils.ReadFromArchive(actualFrame, out _x, out _y);
			}
			catch (InvalidOperationException exc) {
				Debug.LogWarning($"Couldn't read the frame from the depthfile: {exc}");
			}
		}

		if (depths != null) 
			UITextSet.StatusText.text = "read from archive";
		else {
			//Run the model
			if (_dmodel == null) return;
			depths = _dmodel.Run(texture, out _x, out _y);

			/* For a new media, create the depth file */
			if (_depthFilePath == null && !_hasCreatedArchive && _shouldUpdateArchive) {
				DepthFileUtils.CreateDepthFile(_framecount-_startFrame, _startFrame, _hashval, _orig_filepath, _orig_width, _orig_height, _framerate, _x, _y, _dmodel.ModelType);
				_hasCreatedArchive = true;
			}

			//Save it
			if (_shouldUpdateArchive) {
				depths = (float[]) depths.Clone();
				_processedFrames.Add(Task.Run(() => DepthFileUtils.UpdateDepthFile(depths, actualFrame, _x, _y)));
			}

			UITextSet.StatusText.text = "processed";
		}

		if (_ftype == FileTypes.Depth)
			UITextSet.StatusText.text = $"#{actualFrame}/{_framecount-_startFrame}";
		
		_dmesh.SetScene(depths, _x, _y, (float) _orig_width/_orig_height, texture);

		//Check if the params exist in current frame
		if (_paramsDict != null && _paramsDict.ContainsKey(actualFrame))
			_dmesh.ImportParams(_paramsDict[actualFrame]);
	}

	public void HaltVideo() {
		if (_vp == null) return;
		_vp.Stop();
		_vp.url = null;
	}

	private void OnVideoError(VideoPlayer vp, string message) {
		UITextSet.FilepathResultText.text = "Failed to load video: " + message;
		vp.Stop();
		vp.url = "";

		_ftype = FileTypes.Unsupported;
	}
	
	/************************************************************************************/
	/* Depth file input
	/************************************************************************************/

	private void FromDepthFile(string filepath) {
		UITextSet.StatusText.text = "INPUT TEXTURE";

		_recordSize = -1;
		_recording = false;
		_orig_filepath = filepath;

		Debug.Log("Depthfile input: use `send_msg record2048` or `... record4096` to make a sequence of image files");
	}

	private void DepthFileInput(string textureFilepath, FileTypes ftype) {
		/*
		`_orig_filepath` holds the path to the depthfile.
		*/

		/* Invalid filetypes */
		if (ftype != FileTypes.Img && ftype != FileTypes.Vid) {
			UITextSet.StatusText.text = "INVALID INPUT.";
			_ftype = FileTypes.Unsupported;
			return;
		}

		UITextSet.StatusText.text = "INPUT READ.";

		_depthFilePath = _orig_filepath;
		_orig_filepath = textureFilepath;

		/* Read the depthfile */
		string modelType;
		DepthFileUtils.ReadDepthFile(_depthFilePath, out _, out modelType, out _metadata, out _paramsDict, readOnlyMode: true); //let _framecount be read from the texture input
		_hashval = _metadata["hashval"]; //_hashval will use the metadata

		/*
		Should Check:
			hashval
			framecount
			isfull
		*/

		_depthfileCompareText = "";

		/* Check hashval */
#if !UNITY_WEBGL
		bool hashvalEquals = (_hashval == Utils.GetHashval(_orig_filepath));
		_depthfileCompareText += (hashvalEquals) ? "Hashval equals.\n" : "HASHVAL DOES NOT EQUAL.\n";
#endif
		/* Show modeltype */
		_depthfileCompareText += $"Model type: {modelType}\n";

		_recordPath = $"{DepthFileUtils.SaveDir}/recordings/{Utils.GetTimestamp()}";

		//Check if the params exist on the init -- and if it does apply it
		try {
			if (_paramsDict != null && _paramsDict.ContainsKey(-1))
				_dmesh.ImportParams(_paramsDict[-1]);
		}
		catch (Exception exc) {
			Debug.LogError($"FromVideo(): error importing the parameters on init: {exc}");
		}
	
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

			_dmesh.ShouldUpdateDepth = true;

			_orig_width = texture.width;
			_orig_height = texture.height;
			_framecount = 1;

			float[] depths = DepthFileUtils.ReadFromArchive(0, out _x, out _y);
			_dmesh.SetScene(depths, _x, _y, (float) _orig_width/_orig_height, texture);

			/* Set the params for frame 0 (-1 is set above) */
			if (_paramsDict != null && _paramsDict.ContainsKey(0))
				_dmesh.ImportParams(_paramsDict[0]);
			
			DepthFileShowOrRecord();
		}
	}

	private void DepthFileShowOrRecord() {
		//Called after it is ready to show/record

		//print the compretext
		Debug.Log(_depthfileCompareText);

		if (_recordSize <= 0)
			DepthFileShow();
		else
			DepthFileStartRecording(_recordSize);
	}

	private void DepthFileAfterFirstFrame() {
		/* Called after the first frame is received, so that _framecount is set. (by OnFrameReady())*/

		/* Check framecount */
		long framecount_metadata = long.Parse(_metadata["framecount"]);
		long actual_framecount_input = (_framecount - _startFrame);
		long framecountDelta = (actual_framecount_input - framecount_metadata);

		if (framecountDelta == 0) {
			_depthfileCompareText += $"Framecount equals: ({_framecount})\n";
		}
		else if (framecountDelta < 0) { //depthfile has more frames -> leave it be
			_depthfileCompareText += $"FRAMECOUNT DOES NOT EQUAL: (depth > input) : ({framecount_metadata}:{actual_framecount_input})\n";
		}
		else {
			_depthfileCompareText += $"FRAMECOUNT DOES NOT EQUAL: (depth < input) : ({framecount_metadata}:{actual_framecount_input})\n";
			_depthfileCompareText += $"-> #{framecountDelta} FRAMES WILL BE TRIMMED.\n";
			
			_framecount -= framecountDelta;
		}

		/* Check if the depth file is full */
		bool isDepthFull = DepthFileUtils.IsFull();
		_depthfileCompareText += (isDepthFull) ? "Depthfile is full.\n" : "DEPTHFILE IS NOT FULL.\n";

		DepthFileShowOrRecord();
	}

	public void DepthFileStartRecording(int size=2048) {
		_vrrecord.Size = size;

		Utils.CreateDirectory(_recordPath);

		if (_framecount <= 1) {
			//Image --> capture and exit (scene is already set)
			DepthFileCapture();
			UITextSet.StatusText.text = "Captured!";

			_ftype = FileTypes.Unsupported;
			return; //code below will not execused.
		}

		/* Record per frame */
		_recording = true;
		_shouldCapture = false;
		
		_vp.sendFrameReadyEvents = true;
		_vp.Play();
	}

	private void DepthFileFrameReady() {
		_vp.Pause();
		UpdateVid();

		/* Let the mesh update */
		_shouldCapture = true;
	}

	private void UpdateDepthfileInput() {
		if (!_recording || !_shouldCapture)
			return;

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

	private void DepthFileCapture(string format="jpg") =>
		_processedFrames.Add(_vrrecord.Capture($"{_recordPath}/{_currentFrame-_startFrame}.{format}", format));

	private void DepthFileEnded() {
		_recording = false;
		_shouldCapture = false;

		_vp.sendFrameReadyEvents = false;
		_vp.Stop();
		_vp.url = "";

		Task.WaitAll(_processedFrames.ToArray());
		_processedFrames.Clear();

		_ftype = FileTypes.Unsupported;

		UITextSet.StatusText.text = "DONE.";
	}

	/* Switch to video */
	public void DepthFileShow() {
		if (_ftype != FileTypes.Depth) {
			Debug.LogError("_ftype != FileTypes.Depth");
			return;
		}

		UITextSet.OutputSaveText.text = "Not saving.";

		if (_framecount > 1) {
			_ftype = FileTypes.Vid;
			_vp.Play();
		}
		else
			_ftype = FileTypes.Img;
	}

	/************************************************************************************/
	/* End - Depth file input
	/************************************************************************************/

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
		UITextSet.StatusText.text = "Saving."; //Does not work (should be called in update)
		Task.WaitAll(_processedFrames.ToArray());
		_processedFrames.Clear();

		/* Check if it is full */
		bool isFull = DepthFileUtils.IsFull();
		if (isFull) {
			_shouldUpdateArchive = false;
			UITextSet.OutputSaveText.text = "Now full.";
		}

		if (shouldReload) {
			UITextSet.StatusText.text = "Reloading the depthsfile...";
			DepthFileUtils.Reopen();
		}
	}

	public void PausePlay() {
		//Might as well would just delete the call-the-server-on-pause behavior later

		if (_ftype != FileTypes.Vid) return;
		if (_vp == null) return;

		if (_vp.isPaused) {
			/* Play */
			if (_asyncDmodel != null && _asyncDmodel.IsWaiting) {
				UITextSet.StatusText.text = "Waiting for the server.";
				return;
			}

			_dmesh.ShouldUpdateDepth = false;
			_vp.Play();
		}
		else {
			/* Pause */
			_vp.Pause();
			UITextSet.StatusText.text = $"#{_currentFrame-_startFrame}/{_framecount-_startFrame}";

			if (_asyncDmodel != null && _asyncDmodel.IsAvailable &&
				ImgVidDepthGOs.CallServerOnPauseToggle != null && ImgVidDepthGOs.CallServerOnPauseToggle.isOn) 
			{
				_serverTexture = _vp.texture;

				if (_serverTexture != null)
					_asyncDmodel.Run(_serverTexture, OnDepthReady);
			}

			_dmesh.ShouldUpdateDepth = true;
		}
	}

	public void Skip(float seconds) {
		if (_ftype != FileTypes.Vid)
			return;

		if (seconds < 0 && _shouldUpdateArchive) {
			Debug.LogWarning("Can't rewind the video when output is being saved and the depthfile is not full");
			return;
		}

		_vp.time += seconds;
	}

	//Not used anymore
	private void CallPython(string modelType) {
		if (_ftype != FileTypes.Img && _ftype != FileTypes.Vid)
			return;

		const string pythonTarget = @"./depthpy/depth.py";

		string isImage = (_ftype == FileTypes.Img) ? " -i " : " ";
		string modelTypeString = modelType.ToString();

		string depthFilename = DepthFileUtils.GetDepthFileName(Path.GetFileName(_orig_filepath), _hashval,modelType);

		System.Diagnostics.Process.Start(Utils.PythonPath, $"\"{pythonTarget}\" \"{_orig_filepath}\" \"{depthFilename}\" {isImage} -t {modelTypeString} --zip_in_memory");
	}

	private void ExportParams(bool overwrite=false, bool init=false) {
		//If init == true, it will be loaded on the load (and will not be loaded later)

		if (_paramsDict == null)
			_paramsDict = new Dictionary<long, string>();

		long actualFrame;
		if (init) {
			Debug.Log("Exporting params, at init");
			actualFrame = -1;
		}
		else {
			actualFrame = _currentFrame - _startFrame;
			Debug.Log($"Exporting params, #{actualFrame}/{_framecount-_startFrame}");
		}
		
		if (_paramsDict.ContainsKey(actualFrame)) {
			if (overwrite) {
				Debug.Log("Overwriting.");
				_paramsDict.Remove(actualFrame);
			}
			else {
				Debug.LogWarning("Already exists, use `ecf` to overwrite it.");
				return;
			}
		}

		_paramsDict.Add(actualFrame, _dmesh.ExportParams());
		_paramsDictChanged = true;

		if (!_canUpdateArchive)
			Debug.LogWarning("This will not be saved.");
	}

	private void ClearParams() {
		Debug.Log("Clearing the parameters...");
		if (_paramsDict == null) return;

		_paramsDict.Clear();
		_paramsDictChanged = true;

		if (!_canUpdateArchive)
			Debug.LogWarning("This will not be saved.");
	}

	public void SendMsg(string msg) {
		switch (msg) {
		case "Keypad4":
			Skip(-5f);
			break;

		case "Keypad5":
		case "PausePlay":
			PausePlay();
			break;

		case "Keypad6":
			Skip(+5f);
			break;

		case "record2048":
			Debug.Log("Will generate a sequence of images. (2048px)");
			_recordSize = 2048;
			break;
		case "record4096":
			Debug.Log("Will generate a sequence of images. (4096px)");
			_recordSize = 4096;
			break;

		case "CallPythonHybrid":
			CallPython("dpt_large_384");
			break;
		case "CallPythonLarge":
			CallPython("dpt_hybrid_384");
			break;
		
		case "e": //save on the first frame
			ExportParams(overwrite: true, init: true);
			break;

		case "ec": //save on the current frame
			ExportParams(overwrite: false, init: false);
			break;	

		case "ecf": //save on the current frame, force
			ExportParams(overwrite: true, init: false);
			break;
		
		case "eclear": //clear the parameters
			ClearParams();
			break;

		default:
			Debug.LogError("Got unknown msg: " + msg);
			break;
		}
	}

	public void Dispose() {
		_recording = _shouldCapture = false;

		_vp.frameReady -= OnFrameReady;
		_vp.errorReceived -= OnVideoError;
		_vp.loopPointReached -= OnLoopPointReached;

		SaveDepth();
		HaltVideo();

		/* Save the params, if it changed */
		if (_canUpdateArchive && _paramsDictChanged) {
			DepthFileUtils.WriteParams(_paramsDict);
			_paramsDictChanged = false; //not needed
		}

		DepthFileUtils.Dispose();
	}
}
