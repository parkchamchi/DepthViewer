using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

public class ServerConnectBehavior : MonoBehaviour, AsyncDepthModel, CanRunCoroutine {
	public TMP_InputField AddrIF;
	public TMP_InputField ModelTypeIF;
	public TMP_Text ServerStatusText;
	public TMP_Text ModelStatusText;

	public string ModelType {
		get {
			if (_model == null) {
				Debug.LogError("ServerConnectBehavior.ModelType called when _model == null");
				return null;
			}

			return _model.ModelType;
		}
	}

	private DepthServerModel _model;
	public bool IsAvailable {get {return (_model != null);}}
	public bool IsWaiting {get; private set;}

	void Start() {
		IsWaiting = false;
	}

	public void Connect() {
		string addr = AddrIF.text;
		string modelType = ModelTypeIF.text;

		string url = $"{addr}/depthpy/models/{modelType}";
		StartCoroutine(GetRequest(url, modelType));

		ServerStatusText.text = "Connecting.";
	}

	private IEnumerator GetRequest(string url, string modelType) {
		using (UnityWebRequest req = UnityWebRequest.Get(url)) {
			yield return req.SendWebRequest();

			if (req.result == UnityWebRequest.Result.Success) {
				ServerStatusText.text = "OK";
				ModelStatusText.text = url;
				SetModel(url, modelType);
			}
			else {
				ServerStatusText.text = "Failed to connect";
			}
		}
	}

	private void SetModel(string url, string modelType) {
		/* Called by GetRequest() */
		_model = new DepthServerModel(url, modelType, this);
	}

	public void Run(Texture tex, AsyncDepthModel.DepthReadyCallback callback) {
		if (_model == null) return;
		if (IsWaiting) return;

		IsWaiting = true;
		_model.Run(tex, (float[] depths, int x, int y) => {
			IsWaiting = false;
			return callback(depths, x, y);
		});
	}

	public void Disconnect() {
		ServerStatusText.text = "Disconnected.";
		ModelStatusText.text = "";

		_model = null;
		IsWaiting = false;
	}

	public void Dispose() =>
		Disconnect();
}

/* Does not impelement DepthModel */
public class DepthServerModel {
	private string _modelType;
	public string ModelType {get {return _modelType;}}

	private string _url;
	private CanRunCoroutine _behav;
	private AsyncDepthModel.DepthReadyCallback _callback;

	private RenderTexture _rt;

	public DepthServerModel(string url, string modeltype, CanRunCoroutine behav) {
		_url = url + "/pgm";
		_modelType = modeltype;
		_behav = behav;
	}

	public void Run(Texture inTex, AsyncDepthModel.DepthReadyCallback callback) {
		if (inTex == null) {
			Debug.LogError("DepthServerModel.Run(): called when inTex == null");
			_callback(null, 0, 0);
			return;
		}

		_callback = callback;

		Texture2D tex = new Texture2D(inTex.width, inTex.height);
		_rt?.Release();
		_rt = new RenderTexture(inTex.width, inTex.height, 16);
		Graphics.Blit(inTex, _rt);

		RenderTexture.active = _rt;
		tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
		RenderTexture.active = null;

		byte[] jpg = tex.EncodeToJPG();
		UnityEngine.Object.Destroy(tex);

		_behav.StartCoroutine(Post(jpg));
	}

	private IEnumerator Post(byte[] jpg) {
		using (UnityWebRequest req = new UnityWebRequest(_url, UnityWebRequest.kHttpVerbPOST)) {
			UploadHandlerRaw upHandler = new UploadHandlerRaw(jpg);
			req.uploadHandler = upHandler;
			req.downloadHandler = new DownloadHandlerBuffer();

			yield return req.SendWebRequest();

			if (req.result == UnityWebRequest.Result.Success && req.responseCode == 200) {
				byte[] data = req.downloadHandler.data;
				int x, y;
				float[] depths = DepthFileUtils.ReadPGM(data, out x, out y);

				_callback(depths, x, y);
			}
			else {
				//failure
				_callback(null, 0, 0);
			}
		}
	}

	public void Dispose() {
		_url = null; //not needed
		
		_rt?.Release();
		UnityEngine.Object.Destroy(_rt);
	}
}