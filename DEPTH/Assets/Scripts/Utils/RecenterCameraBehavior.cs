using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RecenterCameraBehavior : MonoBehaviour {
	/* Sets TargetParent's transform as TargetCamera's */

	public GameObject TargetParent;
	public Camera TargetCamera;

	//private Vector3 _origPosition;
	private Quaternion _origRotation;

	void Start() {
		//_origPosition = TargetParent.transform.localPosition;
		_origRotation = TargetParent.transform.rotation;
	}

	public void Reset() {
		//TargetParent.transform.localPosition = _origPosition;
		TargetParent.transform.rotation = _origRotation;
	}

	public void Recenter() {
		/* Add offset of position & rotation */
		//TargetParent.transform.localPosition -= TargetCamera.transform.localPosition;
		TargetParent.transform.rotation *=  Quaternion.Inverse(TargetCamera.transform.rotation);
	}
}
