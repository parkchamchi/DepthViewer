using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshParentBehavior : MonoBehaviour {
	/*
		This rotates by mouse, but will restore to (0, 0, 0) by time
	*/

	private const float _acc = 1f;

	private Vector3 _lastMousePos;

	void Update() {
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
}
