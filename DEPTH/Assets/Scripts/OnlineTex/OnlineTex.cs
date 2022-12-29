using System;

using UnityEngine;

public interface OnlineTex : IDisposable {
	public bool Supported {get;}
	public float LastTime {get {return Time.time;}} //The last time it got updated. Always differs for synchronous ones.

	void StartRendering();
	Texture2D GetTex(out int width, out int height);
}