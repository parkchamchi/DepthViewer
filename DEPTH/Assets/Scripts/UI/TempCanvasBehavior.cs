using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;



public class TempCanvasBehavior : MonoBehaviour {
	public GameObject CursorGO;

	private Canvas _canvas;
	private bool _showCursor = false;

	private Vector3 _bottomLeft; //(0, 0)
	private Vector3 _newx, _newy; //contains length

	void Start() {
#if !UNITY_STANDALONE
		/*
		Turns on the (Canvas)'s (Player Input)'s auto-switch on non-standalone builds
		The reason why it's disabled by default is that when a VR controller is detected, it will disable the mouse input, while the VR controller input is not implemented.
		This is primarily for the android build.
		After the VR controller input is implemented, this section will not be needed.
		*/

		PlayerInput pinput = gameObject.GetComponent<PlayerInput>();
		pinput.neverAutoSwitchControlSchemes = false;
#endif	

		_canvas = GetComponent<Canvas>();
	}

	void Update() {
		if (_showCursor) { //if (CursorGO.activeSelf)
			//Get relative mouse position
			Vector3 mousePos = Input.mousePosition;
			float x = mousePos.x / Screen.width;
			float y = mousePos.y / Screen.height;

			Vector3 worldMousePos = _bottomLeft + (_newx * x + _newy * y);

			CursorGO.transform.position = worldMousePos;
		}
	}

	public void VrMode() {
		_canvas.renderMode = RenderMode.WorldSpace;
		transform.localPosition = new Vector3(-10, 0, 0);
		transform.localScale = Vector3.one * 0.01f;
		transform.localRotation = Quaternion.Euler(0, -90, 0);

		Vector3[] corners = new Vector3[4];
		RectTransform rt = GetComponent<RectTransform>();
		rt.GetWorldCorners(corners);

		_bottomLeft = corners[0];
		Vector3 topLeft = corners[1];
		//Vector3 topRight = corners[2];
		Vector3 bottomRight = corners[3];

		_newx = (bottomRight - _bottomLeft);
		_newy = (topLeft - _bottomLeft);

		CursorGO.SetActive(true);
		_showCursor = true;
	}
}
