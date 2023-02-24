using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OnlineTexInputs : TexInputs {
	private OnlineTex _otex;

	private DepthModel _dmodel;
	private IDepthMesh _dmesh;

	private Texture2D _currentTex;
	private float _lastTime;

	public OnlineTexInputs(DepthModel dmodel, IDepthMesh dmesh, OnlineTex otex) {
		_dmodel = dmodel;
		_dmesh = dmesh;

		_lastTime = 0;

		_otex = otex;
		if (!_otex.Supported) {
			Debug.LogError("!_otex.Supported");
			return;
		}

		_otex.StartRendering();
	}

	public void UpdateTex() {
		if (_otex == null || !_otex.Supported)
			return;

		Texture texture = _otex.GetTex(out _, out _);
		if (texture == null) {
			Debug.LogError("Couldn't get the texture");
			return;
		}

		float time = _otex.LastTime;
		if (time == _lastTime) return; //not changed
		_lastTime = time;

		if (_dmodel == null) return;

		Depth depths = _dmodel.Run(texture);
		_dmesh.SetScene(depths, texture);
	}

	public void Dispose() {
		_otex.Dispose();
		_otex = null;
	}
}
