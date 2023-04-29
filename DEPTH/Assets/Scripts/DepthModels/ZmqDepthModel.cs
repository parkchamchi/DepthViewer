using AsyncIO;
using NetMQ;
using NetMQ.Sockets;

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

public class ZmqDepthModel : DepthModel {
	public string ModelType {get; private set;}

	private string _addr;

	private const float _timeout = 3;

	public ZmqDepthModel(string addr="tcp://localhost:5555") {
		Debug.Log($"ZmqDepthModel(): addr: {addr}");
		_addr = addr;
	}

	public Depth Run(Texture inputTexture) {
		/*
		"this line is needed to prevent unity freeze after one use, not sure why yet"
		It's of `AsyncIO`.
		*/
		ForceDotNet.Force();

		using (RequestSocket client = new RequestSocket()) {
			client.Connect(_addr);
			Debug.Log("Connected.");

			Debug.Log("Sending Hello");
			client.SendFrame(
				@"
				ptype=REQ
				name=DEPTH
				!HEADEREND
				asdfasdfajiofds ofiajfn asdfahsi
				"
			);

			string message = null;
			bool gotMessage = false;
			float startTime = Time.realtimeSinceStartup; //Time.time does not update in a single frame
			while (Time.realtimeSinceStartup - startTime < _timeout) {
				gotMessage = client.TryReceiveFrameString(out message);
				if (gotMessage) break;
			}

			if (gotMessage) Debug.Log("Received: " + message);
			else Debug.LogWarning("Timeout.");
		}

		NetMQConfig.Cleanup(); //"this line is needed to prevent unity freeze after one use, not sure why yet"

		return new Depth(new float[] {0, 0, 1, 1}, 2, 2);
	}

	public void Dispose() {

	}
}

