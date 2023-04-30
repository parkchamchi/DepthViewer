using AsyncIO;
using NetMQ;
using NetMQ.Sockets;

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

using Mdict = System.Collections.Generic.Dictionary<string, string>;

public class ZmqDepthModel : SelfDisposingDepthModel {
	public string ModelType {get; private set;}
	public bool IsDisposed {get; private set;} = false;

	private const float _timeout = 2;
	private const int _failTolerance = 3; //Disconnects after n consecutive failures.

	private RequestSocket _socket;
	private Mdict _handshakeMdict;
	private DepthMapType _dtype;
	private System.Action _onDisposedCallback;

	private int _consecutiveFails = 0;

	private RenderTexture _rt;

	public ZmqDepthModel(int port=5555, System.Action onDisposedCallback=null) {
		Debug.Log($"ZmqDepthModel(): port: {port}");
		_onDisposedCallback = onDisposedCallback;

		//"this line is needed to prevent unity freeze after one use, not sure why yet" It's of `AsyncIO`.
		ForceDotNet.Force();
		_socket = new RequestSocket();
		string addr = $"tcp://localhost:{port}";
		Debug.Log($"Connecting to {addr}");
		_socket.Connect(addr);
		Handshake();
	}

	private bool Send(string tosend, out byte[] output) {
		byte[] bytes = Encoding.ASCII.GetBytes(tosend);
		return Send(bytes, out output);
	}

	private bool Send(byte[] tosend, out byte[] output) {
		try {
			_socket.SendFrame(tosend);
		}
		catch (NetMQ.FiniteStateMachineException exc) {
			Debug.LogWarning($"Send(): Failed to send the request. Is the server down?: {exc}");
			output = null;
			return false;
		}

		byte[] message = null;
		bool gotMessage = false;
		float startTime = Time.realtimeSinceStartup; //Time.time does not update in a single frame
		while (Time.realtimeSinceStartup - startTime < _timeout) {
			gotMessage = _socket.TryReceiveFrameBytes(out message);
			if (gotMessage) break;
		}

		if (!gotMessage)
			Debug.LogWarning("Timeout.");

		output = message;
		return gotMessage;
	}

	private void Parse(byte[] bytes, out Mdict mdict, out byte[] data) {
		int idx;
		List<byte> lineList = new List<byte>();

		mdict = new Mdict();
		data = null;

		for (idx = 0; idx < bytes.Length; idx++) {
			if (bytes[idx] != '\n') {
				lineList.Add(bytes[idx]);
				
				//If it's not the end the bytestring, continue
				if (idx != bytes.Length - 1)
					continue;
			}

			//parse lineList
			byte[] lineArray = lineList.ToArray();
			lineList.Clear();
			string line = Encoding.ASCII.GetString(lineArray);
			line = line.Trim();

			//Skip empty lines
			if (line == "")
				continue;

			//Does the line start with '!'?
			if (line.StartsWith('!')) {
				if (line == "!HEADEREND") {
					//The rest it the data
					if (idx != bytes.Length - 1) {
						int dataSize = bytes.Length - 1 - idx; //currently at the newline
						data = new byte[dataSize];
						Array.Copy(bytes, idx + 1, data, 0, dataSize);
					}

					break;
				}
				else {
					Debug.LogWarning($"Unknown line: {line}");
					continue;
				}
			}

			//If it doesn't it's a `key=value` line
			if (line.IndexOf('=') < 0) {
				Debug.LogWarning($"Illegal key-value line: {line}");
				continue;
			}
			
			string[] tokens = line.Split('=', 2); //Max 2
			string key = tokens[0].Trim();
			string value = tokens[1].Trim();
			mdict.Add(key, value);
		}
	}

	private string ReconstructHeader(Mdict mdict) {
		string header = "";
		foreach (KeyValuePair<string, string> item in mdict)
			header += $"{item.Key}={item.Value}\n";

		return header;
	}

	private void GetPtypePname(Mdict mdict, out string ptype, out string pname) {
		ptype = mdict["ptype"];
		pname = mdict["pname"];
	}

	private void OnUnknownPtypePname(Mdict mdict) {
		string ptype, pname;
		GetPtypePname(mdict, out ptype, out pname);
		Debug.LogWarning($"Got unknown (pname, ptype): ({pname}, {ptype})");
	}

	private void OnResError(Mdict mdict, byte[] data) {
		string errorMsg = Encoding.ASCII.GetString(data);
		Debug.LogWarning($"The server responded with the error message: {errorMsg}");
	}

	private void Handshake() {
		Debug.Log("Handshaking...");

		bool success;
		byte[] output = null;

		success = Send(
			@$"
			ptype=REQ
			pname=HANDSHAKE_DEPTH
			
			pversion=1
			client_program=DepthViewer
			client_program_version={DepthFileUtils.Version}
			!HEADEREND",
			out output
		);

		byte[] data;

		if (success) {
			//Check errors... and if there's any problem set success to false
			try {
				Parse(output, out _handshakeMdict, out data);
				Debug.Log(ReconstructHeader(_handshakeMdict));

				string ptype, pname;
				GetPtypePname(_handshakeMdict, out ptype, out pname);

				//Wrong ptype/pname
				if (!(ptype == "RES" && pname == "HANDSHAKE_DEPTH")) {
					if (ptype == "RES" && pname == "ERROR")
						OnResError(_handshakeMdict, data);
					else
						OnUnknownPtypePname(_handshakeMdict);

					success = false;
				}
				else {
					ModelType = _handshakeMdict["model_type"];
					_dtype = (DepthMapType) Enum.Parse(typeof (DepthMapType), _handshakeMdict["depth_map_type"]);
					Debug.Log($"Handshake success. ModelType: {ModelType}");
				}
			}
			catch (Exception exc) {
				Debug.LogError($"Failed to parse: {exc}");
				success = false;
			}
		}
		else
			Debug.LogWarning("The server did not respond.");

		//Cleanup when it failed
		if (!success) {
			Debug.LogWarning("Handshake failure.");
			Dispose();
		}
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

	public Depth Run(Texture inputTexture) {
		if (IsDisposed) {
			Debug.LogError("ZmqDepthModel: This was already disposed. (This should not be seen)");
			return null;
		}

		string inputFormat = "jpg";
		byte[] headerbytes = Encoding.ASCII.GetBytes(
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
		byte[] output;
		success = Send(message, out output);

		Mdict mdict;
		byte[] data;

		if (success) {
			try {
				Parse(output, out mdict, out data);
				
				string ptype, pname;
				GetPtypePname(mdict, out ptype, out pname);

				if (!(ptype == "RES" && pname == "DEPTH")) {
					if (ptype == "RES" && pname == "ERROR")
						OnResError(mdict, data);
					else
						OnUnknownPtypePname(mdict);
				}
				else {
					Depth depth = DepthFileUtils.ReadPgmOrPfm(data, _dtype);
					_consecutiveFails = 0;
					return depth; //Exit normally
				}
			}
			catch (Exception exc) {
				Debug.LogError($"Failed to parse: {exc}");
			}
		}
		else {
			Debug.LogWarning("The server did not respond.");
		}

		//Cleanup
		Debug.Log("ZmqDepthModel(): failed.");
		_consecutiveFails++;
		if (_consecutiveFails > _failTolerance) {
			Debug.Log($"ZmqDepthModel: Disposing after {_consecutiveFails} failures.");
			Dispose();
		}

		return null;
	}

	public void Dispose() {
		_socket.Dispose();
		NetMQConfig.Cleanup(); //"this line is needed to prevent unity freeze after one use, not sure why yet"

		IsDisposed = true;
		if (_onDisposedCallback != null)
			_onDisposedCallback();
	}
}

