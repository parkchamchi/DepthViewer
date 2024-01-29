using IngameDebugConsole;

using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TempCanvasBehavior : MonoBehaviour {
	public GameObject LController, RController;
	private Canvas _canvas;

	void Start() {
		_canvas = GetComponent<Canvas>();

		DebugLogConsole.AddCommandInstance("mesh2con", "Move the mesh to the controller", "MoveMeshToTheController", this);
		DebugLogConsole.AddCommandInstance("movecanvas", "Move the canvas to the world space", "MoveCanvas", this);
	}

	void Update() {
		/*
		if (Input.GetKeyDown(Keymapper.Inst.MoveCanvas))
			MoveCanvas();
		*/
	}

	public void VrMode() {
		//This was for the controller input, but it does not work.

		PlayerInput pinput = gameObject.GetComponent<PlayerInput>();
		pinput.neverAutoSwitchControlSchemes = false;
	}

	public void MoveCanvas() {
		if (_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
			MoveCanvasToWorldSpace();
		else
			MoveCanvasToScreenSpaceOverlay();
	}

	private void MoveCanvasToWorldSpace() {
		_canvas.renderMode = RenderMode.WorldSpace;
		transform.localPosition = new Vector3(10, 0, 0);
		transform.localScale = Vector3.one * 0.01f;
		transform.localRotation = Quaternion.Euler(0, 90, 0);
	}

	private void MoveCanvasToScreenSpaceOverlay() {
		_canvas.renderMode = RenderMode.ScreenSpaceOverlay;
	}

	public void MoveMeshToTheController(bool toRight) {
		//Don't ask me why this is in this file...

		LController.SetActive(true);
		RController.SetActive(true);

		GameObject target = (toRight) ? RController : LController;
		GameObject meshGO = GameObject.Find("DepthPlane");

		meshGO.transform.SetParent(target.transform, false);

		MainBehavior mainbehav = Utils.GetMainBehav();
		mainbehav.SetDof(6);
	}
}
