/*
Parallel to mqpy.py
*/

using AsyncIO;
using NetMQ;
using NetMQ.Sockets;

using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

/*
These lines should be included to the code that uses this file
TODO: just encapsule ALL code of this project into a namespace
*/
//See Mqcs.cs
using Mdict = System.Collections.Generic.Dictionary<string, string>;
using PtypePname = System.Tuple<string, string>;
//using Handler = System.Action<Mdict, byte[]>;
using Handler = System.Action<System.Collections.Generic.Dictionary<string, string>, byte[]>;
//using Handlers = System.Collections.Generic.Dictionary<PtypePname, Handler>;
using Handlers = System.Collections.Generic.Dictionary<System.Tuple<string, string>, System.Action<System.Collections.Generic.Dictionary<string, string>, byte[]>>;

/*
public class PtypePname {
	private string _ptype, _pname;

	public PtypePname(string ptype, string pname) {
		_ptype = ptype;
		_pname = pname;
	}

	public override bool Equals(object o) {
		PtypePname pp = (o as PtypePname);
		if (pp == null) return false;

		return this.ptype == pp.ptype && this.pname == pp.pname;
	}

	public override int GetHashCode() {
		return _ptype.GetHashCode() + _pname.GetHashCode();
	}
}
*/


public class MQ : IDisposable {
	public const int Pversion = 2;

	private Handlers _handlers;
	private RequestSocket _socket;

	private const float _timeout = 2;

	public MQ(Handlers handlers) {
		_handlers = handlers;

		//"this line is needed to prevent unity freeze after one use, not sure why yet" It's of `AsyncIO`.
		ForceDotNet.Force();
	}

	public void Bind(int port) {
		string addr = $"tcp://*:{port}";
		Debug.Log($"Binding to {addr}");
		_socket = new RequestSocket();
		_socket.Bind(addr);
	}

	public void Connect(int port) {
		string addr = $"tcp://localhost:{port}";
		Debug.Log($"Connecting to {addr}");
		_socket = new RequestSocket();
		_socket.Connect(addr);
	}

	public bool Receive() {
		//Returns true on success

		byte[] message = null;
		bool gotMessage = false;
		float startTime = Time.realtimeSinceStartup; //Time.time does not update in a single frame
		while (Time.realtimeSinceStartup - startTime < _timeout) {
			gotMessage = _socket.TryReceiveFrameBytes(out message);
			if (gotMessage) break;
		}

		if (!gotMessage) {
			Debug.LogWarning("Timeout.");
			return false;
		}

		Mdict mdict;
		byte[] data;
		Parse(message, out mdict, out data);
		PtypePname t = new PtypePname(mdict["ptype"], mdict["pname"]);

		Handler handler = OnUnknownPtypePname;
		if (_handlers.ContainsKey(t))
			handler = _handlers[t];
		handler(mdict, data);

		return gotMessage;
	}

	public bool Send(string tosend) =>
		Send(Utils.EncodeAscii(tosend));

	public bool Send(string headerStr, string dataStr) =>
		Send(headerStr, Utils.EncodeUtf8(dataStr));

	public bool Send(string headerStr, byte[] data) =>
		Send(Utils.ConcatByteArray(Utils.EncodeAscii(headerStr), data));

	public bool Send(byte[] tosend) {
		try {
			_socket.SendFrame(tosend);
		}
		catch (Exception exc) when (exc is NetMQ.FiniteStateMachineException || exc is NetMQ.TerminatingException) {
			Debug.LogWarning($"Send(): Failed to send the request. Is the server down?: {exc}");
			return false;
		}

		return true;
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
			string line = Utils.DecodeAscii(lineArray);
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

	private static void GetPtypePname(Mdict mdict, out string ptype, out string pname) {
		ptype = mdict["ptype"];
		pname = mdict["pname"];
	}

	public static string ReconstructHeader(Mdict mdict) {
		string header = "";
		foreach (KeyValuePair<string, string> item in mdict)
			header += $"{item.Key}={item.Value}\n";

		return header;
	}

	public static void OnUnknownPtypePname(Mdict mdict, byte[] _) {
		string ptype, pname;
		GetPtypePname(mdict, out ptype, out pname);
		Debug.LogWarning($"Got unknown (pname, ptype): ({pname}, {ptype})");
	}

	public void Dispose() {
		_socket.Dispose();
		NetMQConfig.Cleanup(); //"this line is needed to prevent unity freeze after one use, not sure why yet"
	}
}