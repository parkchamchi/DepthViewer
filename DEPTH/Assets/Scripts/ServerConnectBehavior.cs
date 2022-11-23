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

	private string _url;

	public void Connect() {
		string addr = AddrIF.text;
		string modelType = ModelTypeIF.text;

		string url = $"{addr}/depthpy/models/{modelType}";
		StartCoroutine(GetRequest(url));
	}

	private IEnumerator GetRequest(string url) {

		using (UnityWebRequest req = UnityWebRequest.Get(url)) {
			yield return req.SendWebRequest();

			if (req.result == UnityWebRequest.Result.Success) {
				ServerStatusText.text = "OK";
				ModelStatusText.text = _url = url;
			}
			else {
				ServerStatusText.text = "Failed to connect";
			}
		}
	}

	public void Disconnect() {
		_url = null;
		ServerStatusText.text = "Disconnected.";
		ModelStatusText.text = "";
	}
}
