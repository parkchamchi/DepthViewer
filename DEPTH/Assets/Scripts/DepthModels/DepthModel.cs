using System;

using UnityEngine;

public interface BaseDepthModel : IDisposable {
	string ModelType {get;}
}

public interface DepthModel : BaseDepthModel {
	float[] Run(Texture inputTexture, out int x, out int y); //value may change after the following calls
	
	float[] RunAndClone(Texture inputTexture, out int x, out int y) {
		return (float[]) Run(inputTexture, out x, out y).Clone();
	}
}

public interface AsyncDepthModel : BaseDepthModel {
	bool IsAvailable {get;}
	bool IsWaiting {get;}

	delegate bool DepthReadyCallback(float[] depths, int x, int y);

	void Run(Texture tex, DepthReadyCallback callback);
}