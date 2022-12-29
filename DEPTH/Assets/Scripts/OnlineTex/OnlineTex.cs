using System;

using UnityEngine;

public interface OnlineTex : IDisposable {
	public bool Supported {get;}

	void StartRendering();
	Texture2D GetTex(out int width, out int height);
}