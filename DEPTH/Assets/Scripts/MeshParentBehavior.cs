using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using IngameDebugConsole;

public class MeshParentBehavior : MonoBehaviour {
	/*
		This rotates by mouse, but will restore to (0, 0, 0) by time
	*/

	private bool _moveMeshByMouse = true;
	private Vector3 _lastMousePos; //can't be null

	void Start() {
		DebugLogConsole.AddCommandInstance("set_moveMeshByMouse", "Whether the mesh would follow the mouse", "SetMoveMeshByMouse", this);
	}

	void Update() {
		if (!_moveMeshByMouse)
			return;

		//Get the diff
		Vector3 currentMousePos = Input.mousePosition;
		Vector3 diff = currentMousePos - _lastMousePos;
		_lastMousePos = currentMousePos;
		
		diff = new Vector3(-diff.y, diff.x) / 32;
		transform.Rotate(diff);
	}

	void FixedUpdate() {
		//Restore
		transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.identity, .05f);
	}

	public void SetMoveMeshByMouse(bool value) {
		Debug.Log($"Setting _moveMeshByMouse to: {value}");
		_moveMeshByMouse = value;
	}
}
