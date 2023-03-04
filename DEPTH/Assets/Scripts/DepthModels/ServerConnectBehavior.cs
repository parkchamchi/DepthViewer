using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

public class ServerConnectBehavior : MonoBehaviour, AsyncDepthModel, CanRunCoroutine {
	public TMP_InputField AddrIF;
	public TMP_Text ServerStatusText;
	public TMP_Text ModelStatusText;

	private DepthServerModel _model;
	public bool IsAvailable {get {return (_model != null);}}
	public bool IsWaiting {get; private set;}

	void Start() {
		IsWaiting = false;
	}

	public void Connect() {
		string addr = AddrIF.text;
		TestConnection(addr);

		ServerStatusText.text = "Connecting.";
	}

	private void TestConnection(string url) {
		//Use the dummy image to check the connection

		Texture2D dummy = Resources.Load<Texture2D>("dummy");
		DepthServerModel testmodel = new DepthServerModel(url, this);
		testmodel.Run(dummy, (Depth depth) => {
			if (depth != null) {
				//success
				ServerStatusText.text = "OK";
				ModelStatusText.text = url;
				SetModel(testmodel);
				return true; //not needed
			}
			else {
				//failure
				ServerStatusText.text = "Failed to connect";
				testmodel.Dispose();
				return false; //not needed
			}
		});
	}

	private void SetModel(DepthServerModel model) {
		_model?.Dispose();
		_model = model;
	}

	public void Run(Texture tex, AsyncDepthModel.DepthReadyCallback callback) {
		if (_model == null) return;
		if (IsWaiting) return;

		IsWaiting = true;
		_model.Run(tex, (Depth depth) => {
			IsWaiting = false;
			return callback(depth);
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
	private string _url;
	private CanRunCoroutine _behav;
	private AsyncDepthModel.DepthReadyCallback _callback;

	private RenderTexture _rt;

	public DepthServerModel(string url, CanRunCoroutine behav) {
		_url = url;
		_behav = behav;
	}

	public void Run(Texture inTex, AsyncDepthModel.DepthReadyCallback callback) {
		if (inTex == null) {
			Debug.LogError("DepthServerModel.Run(): called when inTex == null");
			_callback(null);
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
				Depth depth = DepthFileUtils.ReadPgmOrPfm(data);

				_callback(depth);
			}
			else {
				//failure
				_callback(null);
			}
		}
	}

	public void Dispose() {
		_url = null; //not needed
		
		_rt?.Release();
		UnityEngine.Object.Destroy(_rt);
	}
}