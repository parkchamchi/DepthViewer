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

	private MeshBehavior _dmesh;
	private float _scale {get {return (_dmesh != null) ? _dmesh.Scale : 1;}}

	void Start() {
		DebugLogConsole.AddCommandInstance("set_moveMeshByMouse", "Whether the mesh would follow the mouse", "SetMoveMeshByMouse", this);

		//Find the mesh
		try {
			_dmesh = transform.GetChild(0).GetComponent<MeshBehavior>();
		}
		catch (Exception exc) {
			Debug.LogError($"MeshParentBehavior: couldn't find the mesh: {exc}");
		}
	}

	void Update() {
		if (!_moveMeshByMouse)
			return;

		//Get the diff
		Vector3 currentMousePos = Input.mousePosition;
		Vector3 diff = currentMousePos - _lastMousePos;
		_lastMousePos = currentMousePos;
		
		diff = new Vector3(-diff.y, diff.x) * _scale / 32;
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
