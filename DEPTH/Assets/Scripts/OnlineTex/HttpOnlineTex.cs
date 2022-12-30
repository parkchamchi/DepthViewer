using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Networking;

public class HttpOnlineTex : OnlineTex {
	public bool Supported {get; private set;} = true;
	public float LastTime {get; private set;} = 0;

	private string _url;
	private CanRunCoroutine _behav;

	private bool _isWaiting = false;
	private float _startingTime;
	private Texture2D _currentTex;

	public HttpOnlineTex(string url) {
		_url = url;
		
		_behav = Utils.GetDummyBehavior();
		Supported = (_behav != null);

		_currentTex = StaticGOs.PlaceholderTexture;

		Debug.Log($"Connecting to {_url}");
		UITextSet.StatusText.text = "Connecting...";
	}

	private void Get() {
		if (_isWaiting) return;

		_behav.StartCoroutine(GetRequest());
	}

	private IEnumerator GetRequest() {
		_isWaiting = true;
		_startingTime = Time.time;

		using (UnityWebRequest req = UnityWebRequest.Get(_url)) {
			yield return req.SendWebRequest();

			if (_url == null) yield break; //disposed

			if (req.result == UnityWebRequest.Result.Success) {
				byte[] data = req.downloadHandler.data;
				_currentTex = Utils.LoadImage(data);

				if (_currentTex == null) {
					//not an image file
					UITextSet.StatusText.text = "Failed to parse";
					_currentTex = StaticGOs.PlaceholderTexture;
				}
				else {
					LastTime = Time.time;
					UITextSet.StatusText.text = $"fps: {(int) (1 / (LastTime - _startingTime))}";
				}
			}
			else {
				UITextSet.StatusText.text = "Failed to connect";
			}
		}

		_isWaiting = false;
	}

	public void StartRendering() {}

	public Texture2D GetTex(out int width, out int height) {
		Get();

		width = _currentTex.width;
		height = _currentTex.height;
		return _currentTex;
	}

	public void Dispose() {
		UITextSet.StatusText.text = "Disconnecting.";
		Debug.Log($"Disconnecting from {_url}");

		_isWaiting = true;
		_url = null;
	}
}