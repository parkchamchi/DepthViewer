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
	void Start() {
#if !UNITY_STANDALONE
		PlayerInput pinput = gameObject.GetComponent<PlayerInput>();
		pinput.neverAutoSwitchControlSchemes = false;
#endif	
	}
}
