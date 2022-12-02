/*
Some codes are modified from
https://github.com/WestHillApps/UniGif/blob/master/Assets/UniGif/Example/Script/UniGifImage.cs
(MIT License, see ../UniGif/LICENSE)
*/

using System.Collections;
using System.Collections.Generic;
using System.IO;

using UnityEngine;
using TMPro;

public static class GifPlayer {
	public static int Width {get; private set;}
	public static int Height {get; private set;}
	
	public static int FrameCount {get {
		if (_texlist == null) return 0;
		return _texlist.Count;
	}}
	
	private static int _frame;
	public static int Frame {
		get {
			SetFrame();
			return _frame;
		}
	}

	public enum State {
		None, Loading, Ready, Playing, Paused,
	}
	public static State Status {get; private set;}

	private static CanRunCoroutine _behav;
	private static IEnumerator _coroutine;

	private static List<UniGif.GifTexture> _actualTexlist;
	private static List<UniGif.GifTexture> _texlist {
		get {
			return _actualTexlist;
		}
		set {
			if (_actualTexlist != null)
				ReleaseTexList(_actualTexlist);	
			_actualTexlist = value;
		}
	}
	private static float _lastTime;

	static GifPlayer() {
		Status = State.None;

		_behav = GameObject.Find("DummyObject").GetComponent<DummyBehavior>();
		if (_behav == null) {
			Debug.LogError("Couldn't find the DummyObject");
			
			return;
		}
	}

	public static void FromGif(string filepath) {
		if (!File.Exists(filepath)) {
			Debug.LogError($"File does not exist: {filepath}");
			Status = State.None;
			return;
		}

		Status = State.Loading;

		byte[] bytes = File.ReadAllBytes(filepath);
		_coroutine = UniGif.GetTextureListCoroutine(bytes, OnDecoded);
		_behav.StartCoroutine(_coroutine);
	}

	private static void OnDecoded(List<UniGif.GifTexture> gifTexList, int loopCount, int w, int h) {
		if (Status != State.Loading) {
			//halted elsewhere
			//TODO: check if it's the same file with the filename
			ReleaseTexList(gifTexList);
			return; 
		}

		_texlist = gifTexList;
		Width = w;
		Height = h;

		_frame = 0;
		_lastTime = Time.time;

		Status = State.Playing;
	}

	private static void SetFrame() {
		float delta = Time.time - _lastTime;
		int origFrame = _frame;

		while (delta > _texlist[_frame].m_delaySec) {
			_frame = (++_frame) % FrameCount;
			delta -= _texlist[_frame].m_delaySec;
		}

		if (origFrame != _frame)
			_lastTime = Time.time;
	}

	public static Texture2D GetTexture() {
		if (_texlist == null) {
			Debug.Log($"GifPlayer.GetTexture(): called when _texlist == null");
			return null;
		}

		SetFrame();
		return _texlist[_frame].m_texture2d;
	}

	public static void Release() {
		Width = Height = 0;
		_frame = -1;

		if (_coroutine != null) {
			/* I think a memory leak occurs when the coroutine is stopped before it's fully loaded. */
			//_behav.StopCoroutine(_coroutine);
			_coroutine = null;
		}

		_texlist = null;

		Status = State.None;
	}

	private static void ReleaseTexList(List<UniGif.GifTexture> texlist) {
		foreach (var giftex in texlist)
			UnityEngine.Object.Destroy(giftex.m_texture2d);
	}
}