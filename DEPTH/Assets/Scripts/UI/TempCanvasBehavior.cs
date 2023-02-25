using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;

/*
Turns on the (Canvas)'s (Player Input)'s auto-switch on non-standalone builds
The reason why it's disabled by default is that when a VR controller is detected, it will disable the mouse input, while the VR controller input is not implemented.
This is primarily for the android build.
After the VR controller input is implemented, this script will not be needed.
*/

public class TempCanvasBehavior : MonoBehaviour {
	private Canvas _canvas;

	void Start() {
#if !UNITY_STANDALONE
		PlayerInput pinput = gameObject.GetComponent<PlayerInput>();
		pinput.neverAutoSwitchControlSchemes = false;
#endif	

		_canvas = GetComponent<Canvas>();
	}

	public void VrMode() {
		_canvas.renderMode = RenderMode.WorldSpace;
		transform.localPosition = new Vector3(-10, 0, 0);
		transform.localScale = Vector3.one * 0.01f;
		transform.localRotation = Quaternion.EulerAngles(0, -90, 0);
	}
}
