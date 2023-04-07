using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Keymapper : MonoBehaviour {
	//Alt. for the right click
	public KeyCode HideUI = KeyCode.Escape;

	//Alt. for Scroll wheel
	public KeyCode PrevFileInDir = KeyCode.F1;
	public KeyCode NextFileInDir = KeyCode.F2;

	//Not yet used
	public KeyCode PrevDir = KeyCode.F3;
	public KeyCode NextDir = KeyCode.F4;

	public KeyCode ToggleShuffle = KeyCode.F5;

	public KeyCode VideoRewind = KeyCode.Keypad4;
	public KeyCode VideoPause = KeyCode.Keypad5;
	public KeyCode VideoForward = KeyCode.Keypad6;

	public KeyCode[] DirRandomAccessKeys;

	public static Keymapper Inst {get; private set;}

	void Start() {
		Inst = GameObject.Find("Keymapper").GetComponent<Keymapper>();
		if (Inst == null)
			Debug.LogError("Keymapper.Start(): Couldn't find the Keymapper object.");
	}
}