using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

public class ServerConnectBehavior : MonoBehaviour, CanRunCoroutine {
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

	public int ModelTypeVal {
		get {
			if (_model == null) {
				Debug.LogError("ServerConnectBehavior.ModelTypeVal called when _model == null");
				return 0;
			}

			return _model.ModelTypeVal;
		}
	}

	private DepthServerModel _model;
	public bool IsAvailable {get {return (_model != null);}}

	public void Connect() {
		string addr = AddrIF.text;
		string modelType = ModelTypeIF.text;

		string url = $"{addr}/depthpy/models/{modelType}";
		StartCoroutine(GetRequest(url, modelType));

		ServerStatusText.text = "Connecting.";
	}

	private IEnumerator GetRequest(string url, string modelType) {
		using (UnityWebRequest req = UnityWebRequest.Get(url + "/modeltypeval")) {
			yield return req.SendWebRequest();

			if (req.result == UnityWebRequest.Result.Success) {
				ServerStatusText.text = "OK";
				ModelStatusText.text = url;
				int modelTypeVal = int.Parse(req.downloadHandler.text);
				SetModel(url, modelType, modelTypeVal);
			}
			else {
				ServerStatusText.text = "Failed to connect";
			}
		}
	}

	private void SetModel(string url, string modelType, int modelTypeVal) {
		/* Called by GetRequest() */
		_model = new DepthServerModel(url, modelType, modelTypeVal, this);
	}

	public void Run(Texture tex, DepthServerModel.DepthReadyCallback callback) {
		if (_model == null) return;

		_model.Run(tex, callback);
	}

	public void Disconnect() {
		ServerStatusText.text = "Disconnected.";
		ModelStatusText.text = "";

		_model = null;
	}
}

/* Does not impelement DepthModel */
public class DepthServerModel {
	private string _modelType;
	public string ModelType {get {return _modelType;}}

	private int _modelTypeVal;
	public int ModelTypeVal {get {return _modelTypeVal;}}

	public delegate bool DepthReadyCallback(float[] depths, int x, int y);

	private string _url;
	private CanRunCoroutine _behav;
	private DepthReadyCallback _callback;

	private RenderTexture _rt;

	public DepthServerModel(string url, string modeltype, int modelTypeVal, CanRunCoroutine behav) {
		_url = url + "/pgm";

		_modelTypeVal = modelTypeVal;
		_modelType = modeltype;

		_behav = behav;
	}

	public void Run(Texture inTex, DepthReadyCallback callback) {
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