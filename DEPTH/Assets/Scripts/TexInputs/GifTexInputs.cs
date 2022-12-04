using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GifTexInputs : TexInputs {

	private DepthModel _dmodel;
	private IDepthMesh _dmesh;

	private int _currentFrame;

	public GifTexInputs(string filepath, DepthModel dmodel, IDepthMesh dmesh) {
		_dmodel = dmodel;
		_dmesh = dmesh;

		GifPlayer.FromGif(filepath);
	}

	public void UpdateTex() {
		if (_dmodel == null) return;

		switch (GifPlayer.Status) {
		case GifPlayer.State.None:
			Debug.LogError("GifPlayer Error");
			UITextSet.StatusText.text = "GifPlayer Error";
			return;
		case GifPlayer.State.Loading:
			UITextSet.StatusText.text = "Loading...";
			return;
		case GifPlayer.State.Ready:
			UITextSet.StatusText.text = "Ready.";
			return;
		case GifPlayer.State.Paused:
			UITextSet.StatusText.text = "Paused.";
			return;
		
		case GifPlayer.State.Playing:
			int frame = GifPlayer.Frame;
			if (_currentFrame == frame)
				return;
			_currentFrame = frame;

			Texture2D tex = GifPlayer.GetTexture();
			if (tex == null) return;

			int x, y;
			float[] depths = _dmodel.Run(tex, out x, out y);
			_dmesh.SetScene(depths, x, y, (float) GifPlayer.Width/GifPlayer.Height, tex);

			UITextSet.StatusText.text = $"#{frame}/{GifPlayer.FrameCount}";
			return;

		default:
			UITextSet.StatusText.text = $"DEBUG: GifUpdate: not implemented: {GifPlayer.Status}";
			return;
		}
	}

	public void Dispose() {
		GifPlayer.Release();
		_dmodel = null;
	}

	public bool WaitingSequentialInput {get {return false;}}
	public void SequentialInput(string filepath, FileTypes ftype) {}
}
