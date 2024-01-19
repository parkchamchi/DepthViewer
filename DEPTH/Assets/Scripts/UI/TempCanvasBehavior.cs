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
	}

	public void VrMode() {
		_canvas.renderMode = RenderMode.WorldSpace;
		transform.localPosition = new Vector3(10, 0, 0);
		transform.localScale = Vector3.one * 0.01f;
		transform.localRotation = Quaternion.Euler(0, 90, 0);

		LController.SetActive(true);
		RController.SetActive(true);

		PlayerInput pinput = gameObject.GetComponent<PlayerInput>();
		pinput.neverAutoSwitchControlSchemes = false;
	}
}
