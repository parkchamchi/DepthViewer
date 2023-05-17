using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

public class ZmqTexInputs : TexInputs {
	private IDepthMesh _dmesh;
	//private Depth _depth;

	public ZmqTexInputs(IDepthMesh dmesh, int port) {
		_dmesh = dmesh;
	}

	public void UpdateTex() {}
	public void Dispose() {}
}
