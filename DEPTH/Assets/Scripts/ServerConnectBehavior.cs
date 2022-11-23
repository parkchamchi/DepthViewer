using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;

public class ServerConnectBehavior : MonoBehaviour {
	public TMP_InputField AddrIF;
	public TMP_InputField ModelTypeIF;
	public TMP_Text ServerStatusText;
	public TMP_Text ModelStatusText;

	private MainBehavior _mainBehav;

	void Start() {
		_mainBehav = GameObject.Find("MainManager").GetComponent<MainBehavior>();
	}

	public void Connect() {
		string addr = AddrIF.text;
		string modelType = ModelTypeIF.text;

		ServerStatusText.text = "Connecting.";

		string url = $"{addr}/depthpy/models/{modelType}";
		StartCoroutine(GetRequest(url));
	}

	private IEnumerator GetRequest(string url) {

		using (UnityWebRequest req = UnityWebRequest.Get(url)) {
			yield return req.SendWebRequest();

			if (req.result == UnityWebRequest.Result.Success) {
				ServerStatusText.text = "OK";
				ModelStatusText.text = url;
				int modelTypeVal = int.Parse(req.downloadHandler.text);
				SetModel(url, modelTypeVal);
			}
			else {
				ServerStatusText.text = "Failed to connect";
			}
		}
	}

	private void SetModel(string url, int modelTypeVal) {
		/* Called by GetRequest() */
		_mainBehav.SetModel(new DepthServerModel(url, modelTypeVal));
	}

	public void Disconnect() {
		ServerStatusText.text = "Disconnected.";
		ModelStatusText.text = "";
		_mainBehav.GetBuiltInModel();
	}
}

public class DepthServerModel : DepthModel {
	private int _modelTypeVal;
	public int ModelTypeVal {get;}

	private string _url;
	private DummyBehavior _behav;

	private List<float> _depths;
	private int[] _size;

	public DepthServerModel(string url, int modelTypeVal) {
		_url = url + "/pgm";
		_modelTypeVal = modelTypeVal;

		_behav = GameObject.Find("DummyObject").GetComponent<DummyBehavior>();
	}

	public void Run(Texture inTex, List<float> depths, ref int[] size) {
		_depths = depths;
		_size = size;

		Texture2D tex = new Texture2D(inTex.width, inTex.height);
		RenderTexture rt = new RenderTexture(inTex.width, inTex.height, 16);
		Graphics.Blit(inTex, rt);

		RenderTexture origRT = RenderTexture.active;
		RenderTexture.active = rt;
		tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
		RenderTexture.active = origRT;

		byte[] jpg = tex.EncodeToJPG();
		UnityEngine.Object.Destroy(tex);

		_behav.StartUnityCoroutine(Post(jpg));
	}

	private IEnumerator Post(byte[] jpg) {
		using (UnityWebRequest req = new UnityWebRequest(_url, UnityWebRequest.kHttpVerbPOST)) {
			UploadHandlerRaw upHandler = new UploadHandlerRaw(jpg);
			req.uploadHandler = upHandler;
			req.downloadHandler = new DownloadHandlerBuffer();

			yield return req.SendWebRequest();

			if (req.result == UnityWebRequest.Result.Success) {
				byte[] data = req.downloadHandler.data;
				float[] depths = DepthFileUtils.ReadPGM(data, out _size[0], out _size[1]);

				_depths.Clear();
				_depths.AddRange(depths);
			}
			else {
				Debug.Log("fail");
				_size[0] = -1; //fail signal
			}
		}
	}

	public void Dispose() {
		_url = null; //not needed
	}
}