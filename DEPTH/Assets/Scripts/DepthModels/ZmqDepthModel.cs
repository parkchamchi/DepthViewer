using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
These dll files should be under `DEPTH/Assets/Plugins/NetMQDlls/netstandard2.0`
	NetMQ.dll (4.0.1.12)
	AsyncIO.dll (0.1.69)
	NaCl.dll (0.1.13)
from the nuget files.
*/
/*
Using
	https://github.com/off99555/Unity3D-Python-Communication/blob/master/UnityProject/Assets/NetMQExample/Scripts/HelloRequester.cs
as reference.
*/

//See Mqcs.cs
using Mdict = System.Collections.Generic.Dictionary<string, string>;
using PtypePname = System.Tuple<string, string>;
//using Handler = System.Action<Mdict, byte[]>;
using Handler = System.Action<System.Collections.Generic.Dictionary<string, string>, byte[]>;
//using Handlers = System.Collections.Generic.Dictionary<PtypePname, Handler>;
using Handlers = System.Collections.Generic.Dictionary<System.Tuple<string, string>, System.Action<System.Collections.Generic.Dictionary<string, string>, byte[]>>;

public class ZmqDepthModel : DepthModel {
	public string ModelType {get; private set;}
	public bool IsDisposed {get; private set;} = false;

	private const float _timeout = 2;
	private const int _failTolerance = 3; //Disconnects after n consecutive failures.

	//private RequestSocket _socket;
	//private Mdict _handshakeMdict;
	private MQ _mq;
	private DepthMapType _dtype;
	private System.Action _onDisposedCallback;

	private bool _isHandshaking = false;
	private Depth _depth;
	private int _consecutiveFails = 0;

	private RenderTexture _rt;

	public ZmqDepthModel(int port=5555, System.Action onDisposedCallback=null) {
		Debug.Log($"ZmqDepthModel(): port: {port}");
		_onDisposedCallback = onDisposedCallback;

		_mq = new MQ(new Handlers {
			{new PtypePname("RES", "ERROR"), OnResError},
			{new PtypePname("RES", "HANDSHAKE_DEPTH"), OnResHandshakeDepth},
			{new PtypePname("RES", "DEPTH"), OnResDepth},
		});
		_mq.Connect(port);
		Handshake();
	}

	private void OnResError(Mdict mdict, byte[] data) {
		string errorMsg = Utils.DecodeAscii(data);
		Debug.LogWarning($"The server responded with the error message: {errorMsg}");
		_consecutiveFails++;

		if (_isHandshaking) {
			Debug.LogWarning("Handshake failure.");
			Dispose();
		}
	}

	private void OnResHandshakeDepth(Mdict mdict, byte[] _) {
		Debug.Log(MQ.ReconstructHeader(mdict));

		try {
			ModelType = mdict["model_type"];
			_dtype = (DepthMapType) Enum.Parse(typeof (DepthMapType), mdict["depth_map_type"]);
			Debug.Log($"Handshake success. ModelType: {ModelType}");
		}
		catch (Exception exc) {
			Debug.LogError($"Failed to parse: {exc}");
			Debug.LogWarning("Handshake failure.");
			Dispose();
		}
	}

	private void Handshake() {
		Debug.Log("Handshaking...");
		_isHandshaking = true;

		bool success;

		success = _mq.Send(
			@$"
			ptype=REQ
			pname=HANDSHAKE_DEPTH
			
			pversion={MQ.Pversion}
			client_program=DepthViewer
			client_program_version={DepthFileUtils.Version}
			!HEADEREND"
		);

		if (success) {
			success = _mq.Receive();
			if (!success)
				Debug.LogWarning("The server did not respond.");
		}
		else
			Debug.LogWarning("Failed to send!");

		//Cleanup when it failed
		if (!success) {
			Debug.LogWarning("Handshake failure.");
			Dispose();
		}

		_isHandshaking = false;
	}

	private void OnResDepth(Mdict mdict, byte[] data) {
		_depth = DepthFileUtils.ReadPgmOrPfm(data, _dtype);
		_consecutiveFails = 0;
	}

	public Depth Run(Texture inputTexture) {
		if (IsDisposed) {
			Debug.LogError("ZmqDepthModel: This was already disposed.");
			return null;
		}

		string inputFormat = "jpg";
		byte[] headerbytes = Utils.EncodeAscii(
			$@"
			ptype=REQ
			pname=DEPTH
			input_format={inputFormat}
			!HEADEREND" + "\n"
		);
		byte[] imgbytes = TexToBytes(inputTexture, inputFormat);

		byte[] message = new byte[headerbytes.Length + imgbytes.Length];
		headerbytes.CopyTo(message, 0);
		imgbytes.CopyTo(message, headerbytes.Length);

		bool success;
		success = _mq.Send(message);
		if (success) _mq.Receive();

		if (!success) {
			Debug.LogWarning("The server did not respond.");
			_consecutiveFails++;

			_depth = null;
		}

		//`_consecutiveFails` can be increased elsewhere (OnResError)
		if (_consecutiveFails > _failTolerance) {
			Debug.Log($"ZmqDepthModel: Disposing after {_consecutiveFails} failures.");
			Dispose();

			_depth = null;
		}

		return _depth;
	}

	private byte[] TexToJpg(Texture tex) {
		Texture2D tex2d;
		bool isTex2d = (tex is Texture2D);

		if (isTex2d) {
			tex2d = (Texture2D) tex;
		}
		else {
			/*RenderTexture -> Texture2D*/
			int w = tex.width;
			int h = tex.height;

			//If _rt is not compatible, make a new one
			if (_rt == null || _rt.width != w || _rt.height != h) {
				_rt?.Release();
				_rt = new RenderTexture(w, h, 16);
			}
			Graphics.Blit(tex, _rt); //tex -> _rt

			tex2d = new Texture2D(w, h);
			RenderTexture.active = _rt;
			tex2d.ReadPixels(new Rect(0, 0, w, h), 0, 0); //_rt -> tex2d
			RenderTexture.active = null;
		}

		byte[] bytes = tex2d.EncodeToJPG();

		if (!isTex2d)
			UnityEngine.Object.Destroy(tex2d);

		return bytes;
	}

	private byte[] TexToBytes(Texture tex, string format) {
		switch (format) {
		case "jpg":
			return TexToJpg(tex);

		default:
			Debug.LogError($"TexToBytes(): Got unknown format: {format}");
			return null;
		}
	}

	public void Dispose() {
		_mq.Dispose();

		IsDisposed = true;
		if (_onDisposedCallback != null)
			_onDisposedCallback();
	}
}

